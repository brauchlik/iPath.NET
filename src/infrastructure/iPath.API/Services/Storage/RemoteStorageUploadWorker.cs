using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

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
            var id = await queue.DequeueAsync(stoppingToken);
            try
            {
                // scoped services
                using var scope = sp.CreateAsyncScope();
                using var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
                IRemoteStorageService srv = scope.ServiceProvider.GetRequiredService<IRemoteStorageService>();

                var doc = await db.Documents.Include(d => d.ServiceRequest).SingleOrDefaultAsync(x => x.Id == id, stoppingToken);
                if (doc != null)
                {
                    var res = await (srv.PutFileAsync(doc));
                    if (res.Success)
                    {
                        logger.LogInformation("Document {id} sucessfully put to remote storage", doc.Id);
                    }
                    else
                    {
                        logger.LogWarning("Upload problem with document {id}: {error}", doc.Id, res.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
    }
}
