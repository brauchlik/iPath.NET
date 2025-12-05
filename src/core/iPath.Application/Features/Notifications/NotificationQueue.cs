using iPath.Application.Contracts;
using System.Threading.Channels;

namespace iPath.Application.Features.Notifications;


/*
 * This queue is user to buffer the notifications for delivery to the user
 * ... place then on email queue or send over signalR to the client
 * 
 */


public class NotificationQueue : INotificationQueue
{
    private readonly Channel<Notification> _channel;

    public NotificationQueue(int maxQueueSize)
    {
        _channel = Channel.CreateBounded<Notification>(new BoundedChannelOptions(maxQueueSize)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask EnqueueAsync(Notification item)
    {
        if (item != null)
            await _channel.Writer.WriteAsync(item);
    }

    public async ValueTask<Notification> DequeueAsync(CancellationToken cancellationToken)
    {
        var item = await _channel.Reader.ReadAsync(cancellationToken);
        return item;
    }

    public int QueueSize => _channel.Reader.Count;
}
