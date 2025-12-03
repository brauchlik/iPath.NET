using iPath.Application.Contracts;
using System.Threading.Channels;

namespace iPath.Application.Features.Notifications;

public class NodeNotificationEventQueue : INodeNotificationEventQueue
{
    private readonly Channel<IHasNodeNotification> _channel;

    public NodeNotificationEventQueue(int maxQueueSize)
    {
        _channel = Channel.CreateBounded<IHasNodeNotification>(new BoundedChannelOptions(maxQueueSize)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask EnqueueAsync(IHasNodeNotification item)
    {
        if (item != null)
            await _channel.Writer.WriteAsync(item);
    }

    public async ValueTask<IHasNodeNotification> DequeueAsync(CancellationToken cancellationToken)
    {
        var item = await _channel.Reader.ReadAsync(cancellationToken);
        return item;
    }

    public int QueueSize => _channel.Reader.Count;
}
