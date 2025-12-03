using iPath.Application.Features.Notifications;

namespace iPath.EF.Core.FeatureHandlers.Notifications;

public class NotificationRepository(iPathDbContext db) : INotificationRepository
{
    public Task<PagedResultList<NotificationDto>> GetPage(GetNotificationsQuery query, CancellationToken ct)
    {
        var q = db.NotificationQueue
            .Include(n => n.User)
            .AsNoTracking()
            .Where(n => n.Target == query.Target)
            .OrderBy(n => n.CreatedOn);

        var projected = q.Select(n => new NotificationDto(n.Id, n.CreatedOn, n.EventType, n.Target, n.User.UserName, n.Data));
        return projected.ToPagedResultAsync(query, ct);
    }

    public async Task DeleteAll(CancellationToken ct)
    {
        await db.NotificationQueue.ExecuteDeleteAsync(ct);
    }

    public Task SetReadState(Guid Id, bool IsRead, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
