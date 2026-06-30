using iPath.API.Services.Cache;
using iPath.Application.Features.Admin;
using iPath.Domain.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Jobs;

public class SystemCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly SystemCleanupConfig _config;
    private readonly ILogger<SystemCleanupWorker> _logger;

    public SystemCleanupWorker(
        IServiceProvider sp,
        IOptions<SystemCleanupConfig> config,
        ILogger<SystemCleanupWorker> logger)
    {
        _sp = sp;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SystemCleanupWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateNextDelay(_config.TimeOfDay, DateTime.Now, out var parsedTimeSpan);
            _logger.LogInformation("SystemCleanupWorker: next run scheduled for {Time} (in {Delay:hh\\:mm\\:ss})", 
                DateTime.Now.Date.Add(parsedTimeSpan).Add(delay > TimeSpan.FromDays(0.5) ? TimeSpan.Zero : TimeSpan.FromDays(1)), 
                delay);

            try
            {
                // Wait until the scheduled time of day
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_config.Enabled)
            {
                _logger.LogInformation("SystemCleanupWorker: starting cleanup cycle.");
                try
                {
                    using var scope = _sp.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    // 1. Purge deleted documents
                    if (_config.PurgeDeletedDocuments)
                    {
                        await RunPurgeDeletedDocumentsAsync(mediator, stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("SystemCleanupWorker: PurgeDeletedDocuments is disabled.");
                    }

                    // 2. CacheManager normal eviction (replaces old stale cache logic)
                    if (_config.CleanStaleCache)
                    {
                        await RunCacheEvictionAsync(scope, stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("SystemCleanupWorker: Cache eviction is disabled.");
                    }

                    // 3. Clean conversion staging
                    if (_config.CleanStaging)
                    {
                        await RunCleanStagingAsync(mediator, stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("SystemCleanupWorker: CleanStaging is disabled.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SystemCleanupWorker: error in cleanup cycle.");
                }
            }
            else
            {
                _logger.LogInformation("SystemCleanupWorker: cleanup is disabled in config.");
            }

            // After running, wait a short moment (e.g. 1 minute) before calculating the next delay
            // to ensure we don't accidentally recalculate and run twice in the same minute
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public static TimeSpan CalculateNextDelay(string timeOfDayStr, DateTime now, out TimeSpan parsedTimeSpan)
    {
        if (!TimeSpan.TryParse(timeOfDayStr, out parsedTimeSpan))
        {
            parsedTimeSpan = new TimeSpan(3, 0, 0); // Default to 3 AM
        }

        var targetTime = now.Date.Add(parsedTimeSpan);
        if (now >= targetTime)
        {
            targetTime = targetTime.AddDays(1);
        }

        return targetTime - now;
    }

    private async Task RunPurgeDeletedDocumentsAsync(IMediator mediator, CancellationToken ct)
    {
        _logger.LogInformation("SystemCleanupWorker: Sweeping soft-deleted documents for purging...");
        try
        {
            var deletedDocs = await mediator.Send(new GetDeletedDocumentsWithFilesQuery(), ct);
            if (deletedDocs == null || deletedDocs.Count == 0)
            {
                _logger.LogInformation("SystemCleanupWorker: No soft-deleted documents found to purge.");
                return;
            }

            _logger.LogInformation("SystemCleanupWorker: Found {Count} soft-deleted documents to purge.", deletedDocs.Count);
            foreach (var doc in deletedDocs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    _logger.LogInformation("SystemCleanupWorker: Purging document {DocId} ({Filename})", doc.DocumentId, doc.Filename);
                    var result = await mediator.Send(new PurgeDocumentFilesCommand(doc.DocumentId), ct);
                    if (result)
                    {
                        _logger.LogInformation("SystemCleanupWorker: Successfully purged document {DocId}", doc.DocumentId);
                    }
                    else
                    {
                        _logger.LogWarning("SystemCleanupWorker: PurgeDocumentFilesCommand returned false for {DocId}", doc.DocumentId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SystemCleanupWorker: Failed to purge document {DocId}", doc.DocumentId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SystemCleanupWorker: Error querying soft-deleted documents.");
        }
    }

    private async Task RunCacheEvictionAsync(IServiceScope scope, CancellationToken ct)
    {
        _logger.LogInformation("SystemCleanupWorker: Running cache eviction...");
        try
        {
            var cacheManager = scope.ServiceProvider.GetRequiredService<ICacheManager>();
            await cacheManager.RunNormalEvictionAsync(ct);
            _logger.LogInformation("SystemCleanupWorker: Cache eviction complete.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SystemCleanupWorker: Error during cache eviction.");
        }
    }

    private async Task RunCleanStagingAsync(IMediator mediator, CancellationToken ct)
    {
        _logger.LogInformation("SystemCleanupWorker: Cleaning stale conversion staging folders older than {Days} days...", _config.StaleStagingDays);
        try
        {
            var deletedCount = await mediator.Send(new CleanStaleConversionStagingCommand(_config.StaleStagingDays), ct);
            _logger.LogInformation("SystemCleanupWorker: Staging cleanup complete. Deleted {Count} staging folders.", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SystemCleanupWorker: Error cleaning stale conversion staging.");
        }
    }
}
