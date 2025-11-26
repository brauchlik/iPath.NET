
using System.Threading.Channels;

namespace iPath.API.Services.Uploads;

public class UploadQueue : IUploadQueue
{
    private readonly Channel<Guid> _channel;

    public UploadQueue(int maxQueueSize)
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(maxQueueSize)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public int QueueSize => _channel.Reader.Count;

    public async ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    public async ValueTask EnqueueAsync(Guid item)
    {
        await _channel.Writer.WriteAsync(item);
    }
}
