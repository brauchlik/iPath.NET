using System.Threading.Channels;

namespace iPath.API.Services.Storage;

public class RemoteStorageUploadQueue : IRemoteStorageUploadQueue
{
    private readonly Channel<RemoteStorageCommand> _channel;

    public RemoteStorageUploadQueue(int maxQueueSize)
    {
        _channel = Channel.CreateBounded<RemoteStorageCommand>(new BoundedChannelOptions(maxQueueSize)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public int QueueSize => _channel.Reader.Count;

    public async ValueTask<RemoteStorageCommand> DequeueAsync(CancellationToken ct)
    {
        return await _channel.Reader.ReadAsync(ct);
    }

    public async ValueTask EnqueueAsync(RemoteStorageCommand id, CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(id, ct);
    }
}
