namespace iPath.Application.Contracts;

public interface IRemoteStorageUploadQueue
{
    ValueTask EnqueueAsync(Guid docId, CancellationToken cancellationToken);

    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);

    int QueueSize { get; }
}
