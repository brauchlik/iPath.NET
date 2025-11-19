namespace iPath.Application.Contracts;

public interface INotificationQueue
{
    ValueTask EnqueueAsync(Notification item);

    ValueTask<Notification> DequeueAsync(CancellationToken cancellationToken);

    int QueueSize { get; }
}
