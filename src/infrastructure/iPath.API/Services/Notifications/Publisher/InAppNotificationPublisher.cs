using iPath.Application.Features.Notifications;

namespace iPath.API.Services.Notifications.Publisher;

public class InAppNotificationPublisher(
    ISseConnectionManager sse,
    INotificationEventBus eventBus,
    ILogger<InAppNotificationPublisher> logger)
    : INotificationPublisher
{
    public eNotificationTarget Target => eNotificationTarget.InApp;

    public async Task PublishAsync(Notification n, CancellationToken ct)
    {
        try
        {
            var dto = n.ToDto();
            await sse.SendToUserAsync(n.UserId, "notification", dto, n.CreatedOn.ToString("o"));
            eventBus.PublishNotification(dto);
            n.MarkAsSent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send in-app notification {NotificationId} to user {UserId}", n.Id, n.UserId);
            n.MarkAsFailed(ex.Message);
        }
    }
}
