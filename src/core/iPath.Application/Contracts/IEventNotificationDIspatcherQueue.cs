namespace iPath.Application.Contracts;

public interface IEventNotificationDispatcherQueue
{
    int QueueSize { get; }

    ValueTask<NodeEvent> DequeueAsync(CancellationToken cancellationToken);
    ValueTask EnqueueAsync(NodeEvent item);
}