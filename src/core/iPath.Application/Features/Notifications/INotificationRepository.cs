namespace iPath.Application.Features.Notifications;


public class GetNotificationsQuery : PagedQuery<NotificationDto>
{
    public eNotificationTarget Target { get; set; } = eNotificationTarget.Email;
    public Guid? UserId { get; set; }
}


public interface INotificationRepository
{
    Task<PagedResultList<NotificationDto>> GetPage(GetNotificationsQuery query, CancellationToken ct);
    Task DeleteAll(CancellationToken ct);
    Task SetReadState(Guid Id, bool IsRead, CancellationToken ct);
    Task MarkAllAsRead(Guid userId, CancellationToken ct);
    Task<int> GetUnreadCount(Guid userId, CancellationToken ct);
    Task Delete(Guid id, Guid userId, CancellationToken ct);
}
