
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

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask EnqueueAsync(Guid item)
    {
        throw new NotImplementedException();
    }
}
