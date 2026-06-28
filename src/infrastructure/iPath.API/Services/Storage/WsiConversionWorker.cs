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

public class WsiConversionWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly WsiConversionConfig _config;
    private readonly ILogger<WsiConversionWorker> _logger;

    public WsiConversionWorker(
        IServiceProvider sp,
        IOptions<WsiConversionConfig> config,
        ILogger<WsiConversionWorker> logger)
    {
        _sp = sp;
        _config = config.Value;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WsiConversionWorker starting, reloading pending/active jobs from DB");
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
        var activeJobs = await db.Set<WsiConversionJob>()
            .Where(j => j.Status == WsiConversionStatus.Pending || 
                        j.Status == WsiConversionStatus.Downloading ||
                        j.Status == WsiConversionStatus.Converting ||
                        j.Status == WsiConversionStatus.Uploading)
            .OrderBy(j => j.CreatedOn)
            .ToListAsync(cancellationToken);

        if (activeJobs.Count > 0)
        {
            var queue = _sp.GetRequiredService<IWsiConversionQueue>();
            foreach (var job in activeJobs)
            {
                if (job.Status != WsiConversionStatus.Pending)
                {
                    _logger.LogInformation("Resetting stuck WSI job {DocId} from status {Status} to Pending", job.DocumentId, job.Status);
                    job.Status = WsiConversionStatus.Pending;
                }
                await queue.EnqueueAsync(job.DocumentId, cancellationToken);
            }
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Reloaded {Count} WSI conversion jobs", activeJobs.Count);
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var queue = _sp.GetRequiredService<IWsiConversionQueue>();
                var docId = await queue.DequeueAsync(stoppingToken);
                await ProcessJobAsync(docId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in WsiConversionWorker");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(Guid docId, CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
        var plugins = scope.ServiceProvider.GetRequiredService<IEnumerable<IConversionPlugin>>();

        var job = await db.Set<WsiConversionJob>()
            .Include(j => j.Document)
            .FirstOrDefaultAsync(j => j.DocumentId == docId, ct);

        if (job is null || job.Document is null)
        {
            // Could be a race between scanner saving the job and the worker picking it up from the channel
            for (int i = 0; i < 3; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                job = await db.Set<WsiConversionJob>()
                    .Include(j => j.Document)
                    .FirstOrDefaultAsync(j => j.DocumentId == docId, ct);
                if (job?.Document is not null)
                    break;
            }

            if (job is null || job.Document is null)
            {
                _logger.LogWarning("WsiConversionJob or Document not found for {DocId} after retries", docId);
                return;
            }
        }

        var document = job.Document;

        IConversionPlugin? plugin;
        if (!string.IsNullOrEmpty(job.PluginType))
        {
            plugin = plugins.FirstOrDefault(p => p.GetType().Name == job.PluginType);
        }
        else
        {
            var ext = Path.GetExtension(document.File.Filename ?? "");
            plugin = plugins.FirstOrDefault(p => p.CanHandle(ext));
        }

        if (plugin is null)
        {
            _logger.LogWarning("No conversion plugin found (doc {DocId}, pluginType {PluginType})", docId, job.PluginType);
            job.Status = WsiConversionStatus.Failed;
            job.ErrorMessage = $"No plugin for {(string.IsNullOrEmpty(job.PluginType) ? "ext " + Path.GetExtension(document.File.Filename ?? "") : job.PluginType)}";
            await db.SaveChangesAsync(ct);
            return;
        }

        if (!_config.Enabled && plugin.RequiresConversion)
        {
            _logger.LogWarning("WSI conversion disabled, skipping job for document {DocId}", docId);
            job.Status = WsiConversionStatus.Failed;
            job.ErrorMessage = "WSI conversion is currently disabled";
            document.File.ConversionStatus = DocumentConversionStatus.Failed;
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
                FileExtension: Path.GetExtension(document.File.Filename ?? ""),
                Document: document
            );

            job.Status = WsiConversionStatus.Converting;
            job.StartedOn = DateTime.UtcNow;
            document.File.ConversionStatus = DocumentConversionStatus.Converting;
            await db.SaveChangesAsync(ct);

            var result = await plugin.ProcessAsync(ctx, ct);

            if (result.Success)
            {
                job.Status = WsiConversionStatus.Completed;
                job.CompletedOn = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);
                
                var remoteQueue = _sp.GetRequiredService<IRemoteStorageUploadQueue>();
                await remoteQueue.EnqueueAsync(new RemoteStorageCommand(docId, eRemoteStorageCommand.UploadDocument), ct);

                _logger.LogInformation("VSI conversion completed and remote storage upload enqueued for document {DocId}", docId);
            }
            else
            {
                throw new Exception(result.ErrorMessage ?? "Unknown conversion error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VSI conversion failed for document {DocId}", docId);
            job.Status = WsiConversionStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.RetryCount++;
            document.File.ConversionStatus = DocumentConversionStatus.Failed;
            await db.SaveChangesAsync(ct);

            if (job.RetryCount < _config.MaxRetries)
            {
                job.Status = WsiConversionStatus.Pending;
                document.File.ConversionStatus = DocumentConversionStatus.Pending;
                await db.SaveChangesAsync(ct);
                var queue = _sp.GetRequiredService<IWsiConversionQueue>();
                await queue.EnqueueAsync(docId, ct);
                _logger.LogInformation(
                    "VSI conversion re-queued for document {DocId} (attempt {Retry}/{Max})",
                    docId, job.RetryCount + 1, _config.MaxRetries);
            }
        }
    }
}
