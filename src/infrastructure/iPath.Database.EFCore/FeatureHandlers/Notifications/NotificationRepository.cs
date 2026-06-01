using iPath.Application;
using iPath.Application.Features.Notifications;
using iPath.EF.Core;

namespace iPath.EF.Core.FeatureHandlers.Notifications;

public class NotificationRepository(iPathDbContext db) : INotificationRepository
{
    public async Task<PagedResultList<NotificationDto>> GetPage(GetNotificationsQuery query, CancellationToken ct)
    {
        var q = db.NotificationQueue
            .Include(n => n.User)
            .AsNoTracking()
            .Where(n => n.Target.HasFlag(query.Target));

        if (query.UserId.HasValue)
            q = q.Where(n => n.UserId == query.UserId.Value);

        q = q.ApplyQuery(query, "CreatedOn DESC");

        var projected = q.Select(n => new NotificationDto(n.Id, n.CreatedOn, n.EventType, n.Target,
            new OwnerDto(n.UserId, n.User.UserName, n.User.Email), n.ServiceRequestId, n.EventId, n.Data, n.ReadOn));
        var data = await projected.ToPagedResultAsync(query, ct);
        return data;
    }

    public async Task DeleteAll(CancellationToken ct)
    {
        await db.NotificationQueue.ExecuteDeleteAsync(ct);
    }

    public async Task SetReadState(Guid Id, bool IsRead, CancellationToken ct)
    {
        var notification = await db.NotificationQueue.FindAsync(new object[] { Id }, ct);
        if (notification is null)
            return;

        if (IsRead)
            notification.MarkAsRead();
        else
            notification.MarkAsUnread();

        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsRead(Guid userId, CancellationToken ct)
    {
        await db.NotificationQueue
            .Where(n => n.UserId == userId && n.ReadOn == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadOn, DateTime.UtcNow), ct);
    }

    public async Task<int> GetUnreadCount(Guid userId, CancellationToken ct)
    {
        return await db.NotificationQueue
            .Where(n => n.UserId == userId && n.ReadOn == null)
            .CountAsync(ct);
    }

    public async Task Delete(Guid id, Guid userId, CancellationToken ct)
    {
        await db.NotificationQueue
            .Where(n => n.Id == id && n.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }
}
