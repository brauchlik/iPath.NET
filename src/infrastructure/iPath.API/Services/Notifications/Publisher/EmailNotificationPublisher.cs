using iPath.Application.Features.Notifications;

namespace iPath.API.Services.Notifications.Publisher;

public class EmailNotificationPublisher(IMediator mediator, IEmailSender email, IEnumerable<IServiceRequestHtmlPreview> previews)
    : INotificationPublisher
{
    public eNotificationTarget Target => eNotificationTarget.Email;

    public async Task PublishAsync(Notification n, CancellationToken ct)
    {
        if (n.User is null || !n.User.EmailConfirmed)
        {
            n.MarkAsFailed("User has no confirmed email");
            return;
        }
        if (!n.ServiceRequestId.HasValue)
        {
            n.MarkAsFailed("Service Request not found");
            return;
        }

        var sr = await mediator.Send(new GetServiceRequestByIdQuery(n.ServiceRequestId.Value), ct);
        if (sr is null)
        {
            n.MarkAsFailed("Service Request not found");
            return;
        }

        // prepare email
        var subject = n.EventType switch
        {
            Domain.Notificxations.eNodeNotificationType.NodePublished => "a new case has been published",
            Domain.Notificxations.eNodeNotificationType.NewAnnotation => "a new annotation has been published",
            _ => ""
        };

        var preview = previews.FirstOrDefault(x => x.Name == "email");
        if (preview != null)
        {
            var html = preview.CreatePreview(n.ToDto(), sr);
            if (!string.IsNullOrEmpty(html))
            {
                await email.SendMailAsync(n.User.Email, subject, html);
                n.MarkAsSent();
            }
            else
            {
                n.MarkAsFailed("no html preview generated");
            }
        }
    }

}



