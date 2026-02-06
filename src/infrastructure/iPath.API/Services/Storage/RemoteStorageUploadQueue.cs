using System.Threading.Channels;

namespace iPath.API.Services.Storage;

public class RemoteStorageUploadQueue : IRemoteStorageUploadQueue
{
    private readonly Channel<Guid> _channel;

    public RemoteStorageUploadQueue(int maxQueueSize)
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(maxQueueSize)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public int QueueSize => _channel.Reader.Count;

    public async ValueTask<Guid> DequeueAsync(CancellationToken ct)
    {
        return await _channel.Reader.ReadAsync(ct);
    }

    public async ValueTask EnqueueAsync(Guid id, CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(id, ct);
    }
}
