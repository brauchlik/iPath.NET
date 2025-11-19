using System.Threading.Channels;

namespace iPath.API.Services.Email;


public class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel;

    public EmailQueue(int maxQueueSize)
    {
        _channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(maxQueueSize)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask EnqueueAsync(EmailMessage item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        await _channel.Writer.WriteAsync(item);
    }

    public async ValueTask<EmailMessage> DequeueAsync(CancellationToken cancellationToken)
    {
        var item = await _channel.Reader.ReadAsync(cancellationToken);
        return item;
    }

    public int QueueSize => _channel.Reader.Count;
}
