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

        q = q.ApplyQuery(query, "CreatedOn DESC");

        var projected = q.Select(n => new NotificationDto(n.Id, n.CreatedOn, n.EventType, n.Target, 
            new OwnerDto(n.UserId, n.User.UserName, n.User.Email), n.ServiceRequestId, n.EventId, n.Data));
        var data = await projected.ToPagedResultAsync(query, ct);
        return data;
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
