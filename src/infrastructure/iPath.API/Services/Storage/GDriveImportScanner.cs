using iPath.Domain.Config;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Storage;

public class GDriveImportScanner : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly GDriveImportScannerConfig _config;
    private readonly ILogger<GDriveImportScanner> _logger;

    public GDriveImportScanner(
        IServiceProvider sp,
        IOptions<GDriveImportScannerConfig> config,
        ILogger<GDriveImportScanner> logger)
    {
        _sp = sp;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled || _config.IntervalMinutes <= 0)
        {
            _logger.LogInformation("GDriveImportScanner is disabled");
            return;
        }

        _logger.LogInformation(
            "GDriveImportScanner started. Interval: {Interval} minutes",
            _config.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAllUploadFoldersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GDriveImportScanner scan loop");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_config.IntervalMinutes), stoppingToken);
        }
    }

    private async Task ScanAllUploadFoldersAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IRemoteStorageService>();

        var folders = await db.ServiceRequestUploadFolders
            .Include(f => f.ServiceRequest).ThenInclude(sr => sr.Documents)
            .ToListAsync(ct);

        if (folders.Count == 0)
        {
            _logger.LogDebug("No upload folders to scan");
            return;
        }

        _logger.LogInformation("Scanning {Count} upload folders", folders.Count);

        foreach (var folder in folders)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await storage.ImportUploadFolderAsync(folder, null, ct);
                if (result.ImportCount > 0)
                {
                    _logger.LogInformation(
                        "Imported {Count} documents from upload folder {FolderId}",
                        result.ImportCount, folder.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error importing from upload folder {FolderId}", folder.Id);
            }
        }
    }
}
