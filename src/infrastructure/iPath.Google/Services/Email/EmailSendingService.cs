using DispatchR.Abstractions.Notification;
using iPath.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace iPath.Google.Services.Email;


public class EmailSendingServiceOptions
{
    public int MaxQueueSize { get; set; } = 100;
    public int SleepForSecondsAfterMail { get; set; } = 10;
}


public class EmailSendingService(IOptions<EmailSendingServiceOptions> opts, 
    IServiceProvider services,
    ILogger<EmailSendingService> logger)
    : BackgroundService
    , INotificationHandler<SendMailInput>
{

    private static Channel<SendMailInput> channel;
    private static Channel<SendMailInput> errorQueue;

    public async ValueTask Handle(SendMailInput request, CancellationToken cancellationToken)
    {
        if (channel != null)
        {
            channel.Writer.TryWrite(request);
        }
    }

    private int _sent = 0;
    private int _error = 0;

    public int SentCount => _sent;
    public int ErrorCount => _error;
    public int QueueSize => channel.Reader.Count;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sleepTime = TimeSpan.FromSeconds(opts.Value.SleepForSecondsAfterMail);

        // set up a channel for queing
        channel = Channel.CreateBounded<SendMailInput>(new BoundedChannelOptions(opts.Value.MaxQueueSize)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        errorQueue = Channel.CreateBounded<SendMailInput>(100);

        await Task
                .WhenAny(ProcessChannelAsync(stoppingToken))
                .ConfigureAwait(false);

        async Task ProcessChannelAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var mail = await channel.Reader.ReadAsync(cancellationToken);
                try
                {
                    await using var scope = services.CreateAsyncScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();               
                    await sender.SendMailAsync(mail, cancellationToken);
                    _sent++;
                    await Task.Delay(sleepTime, cancellationToken);
                }
                catch (Exception e)
                {
                    _error++;
                    logger.LogError(e, "An Error Occured processing the email");
                    errorQueue.Writer.TryWrite(mail);
                }
                
            }
        }
    }
}