using iPath.Application.Contracts;

namespace iPath.Application.Features.Notifications;

/*
 * Receive DomainEvents (EventEntity), filter out the NodeEvents
 * and place them on the queue for further processing
 * 
 */

public class NotificationEventHandler(IEventNotificationDispatcherQueue queue)
    : INotificationHandler<EventEntity>
{
    public async ValueTask Handle(EventEntity evt, CancellationToken cancellationToken)
    {
        if (evt is NodeEvent ne)
            await queue.EnqueueAsync(ne);
    }
}