using iPath.API.Services.Notifications.Processors;
using iPath.Application.Coding;
using iPath.Application.Fhir;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iPath.API.Services.Notifications;

/*
 * The ServiceRequestEventProcessor is a background worker that reads the 
 * EventNotificationDispatcherQueue and transforms the events to notifications
 * with the help of an instance of INodeEventProcessor
 * 
 */

public class ServiceRequestEventProcessor(IServiceRequestEventQueue queue,
    ILogger<ServiceRequestEventProcessor> logger,
    IServiceProvider services)
    : BackgroundService
{
    private IServiceScope scope;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            scope = services.CreateScope();
            var dl = scope.ServiceProvider.GetRequiredService<IFhirDataLoader>();
            var coding = scope.ServiceProvider.GetRequiredKeyedService<CodingService>("icdo");
            await coding.LoadCodeSystem();
            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
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

            var processor = scope.ServiceProvider.GetService<IServiceRequestEventProcessor>();
            if (processor != null)
            {
                try
                {
                    await processor.ProcessEvent(evt, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError("Node Event Processing error: {Message}", ex.Message, ex);
                    await Task.Delay(100);
                }
            }
            else
            {
                logger.LogTrace("no event to notification processor for {0}", evt.GetType().Name);
            }

            await Task.Delay(1);
        }
    }
}
