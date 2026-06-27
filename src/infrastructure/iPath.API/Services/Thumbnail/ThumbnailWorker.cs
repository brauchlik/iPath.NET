using iPath.Application.Contracts;
using iPath.Domain.Config;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Thumbnail;

public class ThumbnailWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IThumbnailQueue _queue;
    private readonly VsiConversionConfig _config;
    private readonly iPathConfig _ipathConfig;
    private readonly ILogger<ThumbnailWorker> _logger;

    public ThumbnailWorker(
        IServiceProvider sp,
        IThumbnailQueue queue,
        IOptions<VsiConversionConfig> config,
        IOptions<iPathConfig> ipathConfig,
        ILogger<ThumbnailWorker> logger)
    {
        _sp = sp;
        _queue = queue;
        _config = config.Value;
        _ipathConfig = ipathConfig.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ThumbnailWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var docId = await _queue.DequeueAsync(stoppingToken);
                await ProcessThumbnailAsync(docId, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ThumbnailWorker error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessThumbnailAsync(Guid docId, CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
        var plugins = scope.ServiceProvider.GetRequiredService<IEnumerable<IConversionPlugin>>();

        var doc = await db.Documents.FindAsync([docId], ct);
        if (doc?.File is null)
        {
            _logger.LogWarning("Document {DocId} not found or has no File", docId);
            return;
        }

        // Skip if already has a thumbnail or exceeded retries
        if (!string.IsNullOrEmpty(doc.File.ThumbData))
        {
            _logger.LogDebug("Document {DocId} already has a thumbnail", docId);
            return;
        }
        if (doc.File.ThumbRetryCount >= 3)
        {
            _logger.LogWarning("Document {DocId} exceeded max thumbnail retries", docId);
            return;
        }

        var ext = Path.GetExtension(doc.File.Filename ?? "");
        var plugin = plugins.FirstOrDefault(p => p.CanHandle(ext));
        if (plugin is null)
        {
            _logger.LogDebug("No plugin for extension {Ext} on document {DocId}", ext, docId);
            return;
        }

        // Determine source path: try staging dir first, then TempDataPath
        var stagingPath = string.IsNullOrEmpty(_config.StagingPath)
            ? Path.Combine(_ipathConfig.TempDataPath, "conversion", docId.ToString())
            : Path.Combine(_config.StagingPath, docId.ToString());
        var sourcePath = Path.Combine(stagingPath, doc.File.Filename!);
        if (!File.Exists(sourcePath))
            sourcePath = Path.Combine(_ipathConfig.TempDataPath, docId.ToString());

        var ctx = new ThumbnailContext(docId, sourcePath, _ipathConfig.TempDataPath, 100, doc);

        _logger.LogInformation("Generating thumbnail for document {DocId}", docId);

        var result = await plugin.CreateThumbnailAsync(ctx, ct);

        if (result.Success)
        {
            doc.File.ThumbRetryCount = 0;
            _logger.LogInformation("Thumbnail created for document {DocId}", docId);
        }
        else
        {
            doc.File.ThumbRetryCount++;
            _logger.LogWarning("Thumbnail failed for document {DocId} (attempt {Retry}): {Error}",
                docId, doc.File.ThumbRetryCount, result.ErrorMessage);
        }

        await db.SaveChangesAsync(ct);
    }
}
