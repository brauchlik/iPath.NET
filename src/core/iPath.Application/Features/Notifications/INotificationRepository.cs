namespace iPath.Application.Features.Notifications;


public class GetNotificationsQuery : PagedQuery<NotificationDto>
{
    public eNotificationTarget Target { get; set; } = eNotificationTarget.Email;
}


public interface INotificationRepository
{
    Task<PagedResultList<NotificationDto>> GetPage(GetNotificationsQuery query, CancellationToken ct);
    Task DeleteAll(CancellationToken ct);
    Task SetReadState(Guid Id, bool IsRead, CancellationToken ct);
}
