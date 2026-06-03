using Hl7.Fhir.Model.CdsHooks;
using iPath.API.Services.Notifications.Processors;
using iPath.API.Services.Notifications.Publisher;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iPath.API.Services.Notifications;

public class NotificationPublisher(INotificationQueue queue, 
    IServiceProvider services)
    : BackgroundService
{
    private IServiceScope scope = null!;
    private ILogger<NotificationPublisher> logger = null!;
    private iPathDbContext _db = null!;
    private IMediator _mediator = null!;
    private Dictionary<eNotificationTarget, INotificationPublisher> _publishers = new();

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        scope = services.CreateScope();
        logger = scope.ServiceProvider.GetRequiredService<ILogger<NotificationPublisher>>();
        _mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var sess = scope.ServiceProvider.GetRequiredService<IUserSession>();

        foreach ( var pub in scope.ServiceProvider.GetServices<INotificationPublisher>())
        {
            _publishers.Add(pub.Target, pub);
        }

        // get unsent notifications from database 
        _db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
        var pending = await _db.NotificationQueue.AsNoTracking()
            .Where(n => !n.ProcessedOn.HasValue).Select(n => n.Id)
            .ToListAsync(cancellationToken);
        if (pending.Count > 0)
        {
            logger.LogInformation("NotificationPublisher Start: loading {n} pending notifications", pending.Count);
            foreach (var n in pending)
            {
                await queue.EnqueueAsync(n);
            }
        }
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        scope.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationPublisher Service is running.");
        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var id = await queue.DequeueAsync(stoppingToken);
            logger.LogTrace("processing notification {0}", id);

            try 
            {
                var n = await _db.NotificationQueue
                    .Include(x => x.User)
                    .Where(n => n.Id == id)
                    .SingleOrDefaultAsync(stoppingToken);

                if (!_publishers.ContainsKey(n.Target))
                {
                    n.MarkAsFailed("no publisher for " + n.Target);
                }
                else
                {
                    var pub = _publishers[n.Target];
                    await pub.PublishAsync(n, stoppingToken);                    
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }

            await Task.Delay(1);
        }
    }

}
