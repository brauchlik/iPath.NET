using System.Threading.Channels;
using iPath.Application.Contracts;

namespace iPath.API.Services.Storage;

public class VsiConversionQueue : IVsiConversionQueue
{
    private readonly Channel<Guid> _channel;

    public VsiConversionQueue(int maxQueueSize = 10)
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(maxQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public int QueueSize => _channel.Reader.Count;

    public async ValueTask<Guid> DequeueAsync(CancellationToken ct)
    {
        return await _channel.Reader.ReadAsync(ct);
    }

    public async ValueTask EnqueueAsync(Guid documentId, CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(documentId, ct);
    }
}
