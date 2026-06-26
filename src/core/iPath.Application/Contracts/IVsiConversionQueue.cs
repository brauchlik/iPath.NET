namespace iPath.Application.Contracts;

public interface IVsiConversionQueue
{
    ValueTask EnqueueAsync(Guid documentId, CancellationToken ct);

    ValueTask<Guid> DequeueAsync(CancellationToken ct);

    int QueueSize { get; }
}
