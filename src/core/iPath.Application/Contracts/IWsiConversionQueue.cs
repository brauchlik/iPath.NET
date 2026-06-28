namespace iPath.Application.Contracts;

public interface IWsiConversionQueue
{
    ValueTask EnqueueAsync(Guid documentId, CancellationToken ct);

    ValueTask<Guid> DequeueAsync(CancellationToken ct);

    int QueueSize { get; }
}
