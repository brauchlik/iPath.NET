using iPath.API.Services.Notifications.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iPath.API.Services.Notifications;

/*
 * The EventNotificationDispatcher is a background worker that reads the 
 * EventNotificationDispatcherQueue and transforms the events to notifications
 * with the help of an instance of INodeEventProcessor
 * 
 */

public class EventNotificationDispatcher(IEventNotificationDispatcherQueue queue,
    ILogger<EventNotificationDispatcher> logger,
    IServiceProvider services)
    : BackgroundService
{
    private IServiceScope scope;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        scope = services.CreateScope();
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        scope.Dispose();
        scope = null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationQueueWorker Hosted Service is running.");
        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var evt = await queue.DequeueAsync(stoppingToken);

            logger.LogTrace("processing event type {0}", evt.GetType().Name);

            var processor = scope.ServiceProvider.GetService<INodeEventProcessor>();
            if (processor != null)
            {
                try
                {
                    await processor.ProcessEvent(evt, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError("Node Event Processing error: {Message}", ex.Message, ex);
                }
            }
            else
            {
                logger.LogTrace("no event to notification processor for {0}", evt.GetType().Name);
            }

            await Task.Delay(100);
        }
    }
}
