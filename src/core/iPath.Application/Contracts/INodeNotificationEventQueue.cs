namespace iPath.Application.Contracts;

public interface INodeNotificationEventQueue
{
    int QueueSize { get; }

    ValueTask<IHasNodeNotification> DequeueAsync(CancellationToken cancellationToken);
    ValueTask EnqueueAsync(IHasNodeNotification item);
}