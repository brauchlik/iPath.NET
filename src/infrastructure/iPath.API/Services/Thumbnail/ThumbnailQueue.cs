using System.Threading.Channels;
using iPath.Application.Contracts;

namespace iPath.API.Services.Thumbnail;

public class ThumbnailQueue : IThumbnailQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    private readonly HashSet<Guid> _active = [];

    public int QueueSize => _channel.Reader.Count;

    public ValueTask EnqueueAsync(Guid docId, CancellationToken ct)
    {
        lock (_active)
        {
            if (!_active.Add(docId))
                return ValueTask.CompletedTask;
        }
        return _channel.Writer.WriteAsync(docId, ct);
    }

    public async ValueTask<Guid> DequeueAsync(CancellationToken ct)
    {
        var docId = await _channel.Reader.ReadAsync(ct);
        lock (_active) { _active.Remove(docId); }
        return docId;
    }
}
