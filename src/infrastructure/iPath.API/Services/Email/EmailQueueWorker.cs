using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iPath.API.Services.Email;

public class EmailQueueWorker(IEmailQueue queue,
    ILogger<EmailQueueWorker> logger,
    IServiceProvider services)
    : BackgroundService
{
    private IServiceScope scope;
    private IEmailSender srv;
    private IMediator mediator;


    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        scope = services.CreateScope();
        srv = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        scope.Dispose();
        scope = null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationQueueWorker Hosted Service is running.");
        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var mail = await queue.DequeueAsync(stoppingToken);
            var res = await srv.SendMailAsync(mail.Receiver, mail.Subject, mail.Body);

            if (res.IsSuccess)
            {
                await mediator.Send(new EmailSetSentCommand(mail.Id), stoppingToken);
            }
            else
            {
                await mediator.Send(new EmailSetErrorCommand(mail.Id, res.ErrorMessage()), stoppingToken);
            }

            await Task.Delay(500);
        }
    }

}
