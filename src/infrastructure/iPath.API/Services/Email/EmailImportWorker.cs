using iPath.Application.Contracts;
using iPath.Application.Features.EmailImport;
using iPath.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Email;

public class EmailImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EmailImportConfig _config;
    private readonly ILogger<EmailImportWorker> _logger;

    public EmailImportWorker(
        IServiceProvider serviceProvider,
        IOptions<EmailImportConfig> config,
        ILogger<EmailImportWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailImportWorker started. Enabled: {Enabled}, Interval: {Interval} minutes", 
            _config.Enabled, _config.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_config.Enabled && _config.Mailboxes.Count > 0)
            {
                try
                {
                    await ImportAllMailboxesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during scheduled email import");
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(_config.IntervalMinutes), stoppingToken);
        }
    }

    private async Task ImportAllMailboxesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var importService = scope.ServiceProvider.GetRequiredService<IEmailImportService>();

        _logger.LogInformation("Starting scheduled email import for {Count} mailboxes", _config.Mailboxes.Count);

        var results = await importService.ImportAllPendingAsync(ct);

        var imported = results.Count(r => r.Status == EmailImportStatus.Imported);
        var quarantined = results.Count(r => r.Status == EmailImportStatus.Quarantined);
        var failed = results.Count(r => r.Status == EmailImportStatus.Failed);

        _logger.LogInformation("Email import completed. Imported: {Imported}, Quarantined: {Quarantined}, Failed: {Failed}",
            imported, quarantined, failed);
    }
}