using iPath.Application.Contracts;
using System.Threading.Channels;

namespace iPath.Application.Features.Notifications;


/*
 * This queue is used to transform DomainEvents of type NodeEvent to Notifications
 * Events are handled by NotificationEventHandler and then placed on this queue
 * for further processing => filter by user subscription, etc ...
 * 
 */

public class EventNotificationDispatcherQueue : IEventNotificationDispatcherQueue
{
    private readonly Channel<NodeEvent> _channel;

    public EventNotificationDispatcherQueue(int maxQueueSize)
    {
        _channel = Channel.CreateBounded<NodeEvent>(new BoundedChannelOptions(maxQueueSize)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask EnqueueAsync(NodeEvent item)
    {
        if (item != null)
            await _channel.Writer.WriteAsync(item);
    }

    public async ValueTask<NodeEvent> DequeueAsync(CancellationToken cancellationToken)
    {
        var item = await _channel.Reader.ReadAsync(cancellationToken);
        return item;
    }

    public int QueueSize => _channel.Reader.Count;
}
