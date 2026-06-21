using System.Collections.Concurrent;
using System.Threading.Channels;
using iPath.Application.AI;

namespace iPath.Database.EFCore.AI;

public class AiTranscriptionQueue : IAiTranscriptionQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<Guid, bool> _activeQueue = new();

    public async ValueTask EnqueueAsync(Guid caseId)
    {
        if (_activeQueue.TryAdd(caseId, true))
        {
            await _channel.Writer.WriteAsync(caseId);
        }
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }

    public bool IsInQueue(Guid caseId)
    {
        return _activeQueue.ContainsKey(caseId);
    }

    public void Dequeue(Guid caseId)
    {
        _activeQueue.TryRemove(caseId, out _);
    }

    public int GetQueueCount() => _activeQueue.Count;
}
