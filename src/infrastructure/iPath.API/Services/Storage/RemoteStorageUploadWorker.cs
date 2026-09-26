using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Storage;

public class RemoteStorageUploadWorker(IServiceProvider sp)
    : BackgroundService
{
    private IRemoteStorageUploadQueue queue;
    private ILogger<RemoteStorageUploadWorker> logger;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // singleton services
        queue = sp.GetRequiredService<IRemoteStorageUploadQueue>();
        logger = sp.GetRequiredService<ILogger<RemoteStorageUploadWorker>>();

        await RequeuePendingUploadsAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    // The queue lives in memory. A document whose temp file exists but which is not stored yet
    // was uploaded before a restart and is queued again, so the temp copy is never the last one.
    private async Task RequeuePendingUploadsAsync(CancellationToken ct)
    {
        try
        {
            var tempPath = sp.GetRequiredService<IOptions<iPathConfig>>().Value.TempDataPath;
            if (string.IsNullOrEmpty(tempPath) || !Directory.Exists(tempPath))
                return;

            var ids = Directory.EnumerateFiles(tempPath)
                .Select(Path.GetFileName)
                .Select(n => Guid.TryParse(n, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();
            if (ids.Count == 0)
                return;

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
            var pending = (await db.Documents.AsNoTracking()
                    .Where(d => ids.Contains(d.Id))
                    .Select(d => new { d.Id, d.File })
                    .ToListAsync(ct))
                // Slides still converting are uploaded by the conversion worker when it finishes.
                .Where(d => d.File?.Storage is null
                    && d.File?.ConversionStatus is null or DocumentConversionStatus.Completed)
                .Select(d => d.Id)
                .ToList();

            foreach (var id in pending)
                await queue.EnqueueAsync(new RemoteStorageCommand(id, eRemoteStorageCommand.UploadDocument), ct);
            if (pending.Count > 0)
                logger.LogInformation("Re-queued {Count} uploads that did not finish before the last shutdown", pending.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not re-queue pending uploads");
        }
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var cmd = await queue.DequeueAsync(stoppingToken);
            if (cmd is not null)
            {
                try
                {
                    // scoped services
                    using var scope = sp.CreateAsyncScope();
                    using var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
                    IRemoteStorageService srv = scope.ServiceProvider.GetRequiredService<IRemoteStorageService>();

                    var res = cmd.command switch
                    {
                        eRemoteStorageCommand.UploadDocument => await srv.PutFileAsync(cmd.objId, stoppingToken),
                        eRemoteStorageCommand.DeleteDocument => await srv.DeleteFileAsync(cmd.objId, stoppingToken),
                        eRemoteStorageCommand.FetchDocument => await srv.GetFileAsync(cmd.objId, stoppingToken),
                        eRemoteStorageCommand.UploadServiceRequest => await srv.PutServiceRequestJsonAsync(cmd.objId, stoppingToken),
                        eRemoteStorageCommand.DeleteServiceRequest => await srv.DeleteServiceRequestJsonAsync(cmd.objId, stoppingToken),
                        _ => StorageRepsonse.Fail($"Unknown storage command {cmd.command}"),
                    };

                    if (res.Success)
                    {
                        logger.LogInformation("{cmd} {id} sucessfull", cmd.command, cmd.objId);
                    }
                    else
                    {
                        logger.LogWarning("{cmd} {id} failed: {err}", cmd.command, cmd.objId, res.Message);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
            }
            else
            {
                await Task.Delay(5000);
            }
        }
    }
}
