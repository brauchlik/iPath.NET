using iPath.Application.Features.Notifications;
using iPath.Application.Features.ServiceRequests;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests;

public class GetServiceRequestEventsQueryHandler(iPathDbContext db)
    : IRequestHandler<GetServiceRequestEventsQuery, Task<List<EventEntity>>>
{
    public async Task<List<EventEntity>> Handle(GetServiceRequestEventsQuery request, CancellationToken ct)
    {
        return await db.EventStore
            .Where(e => e.ObjectId == request.ServiceRequestId)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync(ct);
    }
}

public class GetServiceRequestNotificationsQueryHandler(iPathDbContext db)
    : IRequestHandler<GetServiceRequestNotificationsQuery, Task<List<NotificationDto>>>
{
    public async Task<List<NotificationDto>> Handle(GetServiceRequestNotificationsQuery request, CancellationToken ct)
    {
        var notifications = await db.NotificationQueue
            .Include(n => n.User)
            .Where(n => n.ServiceRequestId == request.ServiceRequestId)
            .OrderByDescending(n => n.CreatedOn)
            .Select(n => new NotificationDto(
                n.Id,
                n.CreatedOn,
                n.EventType,
                n.Target,
                new OwnerDto(n.UserId, n.User != null ? n.User.UserName : null, n.User != null ? n.User.Email : null),
                n.ServiceRequestId,
                n.EventId,
                null
            ))
            .ToListAsync(ct);

        return notifications;
    }
}