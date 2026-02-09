using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iPath.API.Services.Storage;

public class RemoteStorageUploadWorker(IServiceProvider sp)
    : BackgroundService
{
    private IRemoteStorageUploadQueue queue;
    private ILogger<RemoteStorageUploadWorker> logger;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // singleton services
        queue = sp.GetRequiredService<IRemoteStorageUploadQueue>();
        logger = sp.GetRequiredService<ILogger<RemoteStorageUploadWorker>>();

        return base.StartAsync(cancellationToken);
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
                        eRemoteStorageCommand.DeleteServiceRequest => await srv.DeleteFileAsync(cmd.objId, stoppingToken),
                        eRemoteStorageCommand.UploadServiceRequest => await srv.PutServiceRequestJsonAsync(cmd.objId, stoppingToken)
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
