namespace iPath.Application.Contracts;

public interface IEmailQueue
{
    ValueTask EnqueueAsync(EmailMessage item);

    ValueTask<EmailMessage> DequeueAsync(CancellationToken cancellationToken);

    int QueueSize { get; }
}
