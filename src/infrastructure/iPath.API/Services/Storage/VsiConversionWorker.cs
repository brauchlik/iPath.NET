using iPath.Application.Contracts;
using iPath.Application.Features.Conversion;
using iPath.Domain.Config;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Storage;

public class VsiConversionWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly VsiConversionConfig _config;
    private readonly ILogger<VsiConversionWorker> _logger;

    public VsiConversionWorker(
        IServiceProvider sp,
        IOptions<VsiConversionConfig> config,
        ILogger<VsiConversionWorker> logger)
    {
        _sp = sp;
        _config = config.Value;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_config.Enabled)
        {
            _logger.LogInformation("VsiConversionWorker starting, reloading pending/active jobs from DB");
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
            var activeJobs = await db.Set<VsiConversionJob>()
                .Where(j => j.Status == VsiConversionStatus.Pending || 
                            j.Status == VsiConversionStatus.Downloading ||
                            j.Status == VsiConversionStatus.Converting ||
                            j.Status == VsiConversionStatus.Uploading)
                .OrderBy(j => j.CreatedOn)
                .ToListAsync(cancellationToken);

            if (activeJobs.Count > 0)
            {
                var queue = _sp.GetRequiredService<IVsiConversionQueue>();
                foreach (var job in activeJobs)
                {
                    if (job.Status != VsiConversionStatus.Pending)
                    {
                        _logger.LogInformation("Resetting stuck VSI job {DocId} from status {Status} to Pending", job.DocumentId, job.Status);
                        job.Status = VsiConversionStatus.Pending;
                    }
                    await queue.EnqueueAsync(job.DocumentId, cancellationToken);
                }
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Reloaded {Count} VSI conversion jobs", activeJobs.Count);
            }
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var queue = _sp.GetRequiredService<IVsiConversionQueue>();
                var docId = await queue.DequeueAsync(stoppingToken);
                await ProcessJobAsync(docId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in VsiConversionWorker");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(Guid docId, CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
        var plugins = scope.ServiceProvider.GetRequiredService<IEnumerable<IConversionPlugin>>();

        var job = await db.Set<VsiConversionJob>()
            .Include(j => j.Document)
            .FirstOrDefaultAsync(j => j.DocumentId == docId, ct);

        if (job is null || job.Document is null)
        {
            _logger.LogWarning("VsiConversionJob or Document not found for {DocId}", docId);
            return;
        }

        var document = job.Document;
        var ext = Path.GetExtension(document.File.Filename ?? "");
        var plugin = plugins.FirstOrDefault(p => p.CanHandle(ext));

        if (plugin is null)
        {
            _logger.LogWarning("No conversion plugin found for extension {Ext} (doc {DocId})", ext, docId);
            job.Status = VsiConversionStatus.Failed;
            job.ErrorMessage = $"No plugin for extension {ext}";
            await db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            _logger.LogInformation("VSI conversion started for document {DocId} ({Filename})", docId, document.File.Filename);

            var stagingPath = job.OriginalStorageId;
            if (string.IsNullOrEmpty(stagingPath) || !Directory.Exists(stagingPath))
            {
                stagingPath = Path.Combine(
                    string.IsNullOrEmpty(_config.StagingPath)
                        ? Path.GetTempPath()
                        : _config.StagingPath,
                    docId.ToString());
            }

            if (!Directory.Exists(stagingPath))
            {
                throw new Exception($"Staging path does not exist: {stagingPath}");
            }

            var ctx = new ConversionJobContext(
                DocumentId: docId,
                StagingPath: stagingPath,
                OriginalFilename: document.File.Filename ?? "slide",
                FileExtension: ext,
                Document: document
            );

            job.Status = VsiConversionStatus.Converting;
            job.StartedOn = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var result = await plugin.ProcessAsync(ctx, ct);

            if (result.Success)
            {
                job.Status = VsiConversionStatus.Completed;
                job.CompletedOn = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("VSI conversion completed for document {DocId}", docId);
            }
            else
            {
                throw new Exception(result.ErrorMessage ?? "Unknown conversion error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VSI conversion failed for document {DocId}", docId);
            job.Status = VsiConversionStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.RetryCount++;
            await db.SaveChangesAsync(ct);

            if (job.RetryCount < _config.MaxRetries)
            {
                job.Status = VsiConversionStatus.Pending;
                await db.SaveChangesAsync(ct);
                var queue = _sp.GetRequiredService<IVsiConversionQueue>();
                await queue.EnqueueAsync(docId, ct);
                _logger.LogInformation(
                    "VSI conversion re-queued for document {DocId} (attempt {Retry}/{Max})",
                    docId, job.RetryCount + 1, _config.MaxRetries);
            }
        }
    }
}
