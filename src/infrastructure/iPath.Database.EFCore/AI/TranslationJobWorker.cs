using System.Linq;
using iPath.Application.Features.Admin;
using iPath.Application.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.Database.EFCore.AI;

public class TranslationJobWorker : BackgroundService
{
    private readonly ITranslationJobQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TranslationJobWorker> _logger;
    private readonly TimeSpan _batchWindow = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(2);
    private const int MaxBatchSize = 5;

    public TranslationJobWorker(
        ITranslationJobQueue queue,
        IServiceProvider serviceProvider,
        ILogger<TranslationJobWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TranslationJobWorker background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_queue.IsPaused)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TranslationJobWorker");
                await Task.Delay(_retryDelay, stoppingToken);
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        // Wait for at least one key before starting the batch timer
        var firstKey = await _queue.WaitToReadAsync(ct);
        if (firstKey == null) return;

        var batch = new List<string>(MaxBatchSize) { firstKey };

        // Accumulate more keys within the batch window
        using var timeout = new CancellationTokenSource(_batchWindow);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        while (batch.Count < MaxBatchSize)
        {
            var next = await _queue.WaitToReadAsync(linked.Token);
            if (next == null) break;
            batch.Add(next);
        }

        // Deduplicate key batch to avoid redundant LLM queries
        var uniqueKeys = batch.Distinct().ToList();

        if (_queue.IsPaused)
        {
            _logger.LogInformation("Translation queue is paused. Re-enqueuing {Count} keys.", uniqueKeys.Count);
            foreach (var key in uniqueKeys)
            {
                _queue.EnqueueKey(key);
            }
            return;
        }

        _logger.LogInformation("Translation job: batch of {Count} key(s) (deduplicated to {UniqueCount})", batch.Count, uniqueKeys.Count);

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var locOpts = scope.ServiceProvider.GetRequiredService<IOptions<LocalizationSettings>>();

        var locales = locOpts.Value.SupportedCultures.Where(c => c != "en").ToList();
        bool anyFailed = false;

        foreach (var locale in locales)
        {
            try
            {
                var command = new TranslateKeysBatchCommand(locale, uniqueKeys);
                var result = await mediator.Send(command, ct);

                if (result.IsSuccess && result.TranslatedCount > 0)
                {
                    _logger.LogInformation("Translated {Count} keys for locale '{Locale}'", result.TranslatedCount, locale);
                }
                else if (!result.IsSuccess)
                {
                    _logger.LogWarning("Translation failed for locale '{Locale}': {Error}", locale, result.ErrorMessage);
                    anyFailed = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Translation error for locale '{Locale}' — model may be offline", locale);
                anyFailed = true;
            }
        }

        if (anyFailed)
        {
            _logger.LogInformation("Translation job paused {Seconds}s after failure", _retryDelay.TotalSeconds);
            await Task.Delay(_retryDelay, ct);
        }
    }
}
