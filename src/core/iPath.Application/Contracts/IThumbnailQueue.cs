namespace iPath.Application.Contracts;

public interface IThumbnailQueue
{
    ValueTask EnqueueAsync(Guid documentId, CancellationToken ct);
    ValueTask<Guid> DequeueAsync(CancellationToken ct);
    int QueueSize { get; }
}
