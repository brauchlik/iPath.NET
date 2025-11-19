namespace iPath.Application.Contracts;

public interface IUploadQueue
{
    ValueTask EnqueueAsync(Guid item);

    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);

    int QueueSize { get; }
}
