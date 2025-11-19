namespace iPath.API.Services.Notifications;

public interface INotificationProcessor
{
    NotificationType notificationType { get; }
    Task<Result> ProcessNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
}
