using iPath.Application.Features.Admin;
using iPath.Application.Features.Notifications;
using iPath.Application.Features.ServiceRequests;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests;

public class GetServiceRequestEventsQueryHandler(iPathDbContext db)
    : IRequestHandler<GetServiceRequestEventsQuery, Task<List<EventDto>>>
{
    public async Task<List<EventDto>> Handle(GetServiceRequestEventsQuery request, CancellationToken ct)
    {
        var events = await db.EventStore
            .Where(e => e.ObjectId == request.ServiceRequestId)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync(ct);

        var eventIds = events.Select(e => e.EventId).ToList();
        
        var notificationCounts = await db.NotificationQueue
            .Where(n => n.EventId != null && eventIds.Contains(n.EventId!.Value))
            .GroupBy(n => n.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId!.Value, x => x.Count, ct);

        return events.Select(e => new EventDto(
            e.EventId,
            e.EventDate,
            e.UserId,
            e.EventName,
            e.ObjectName,
            e.ObjectId,
            notificationCounts.GetValueOrDefault(e.EventId, 0)
        )).ToList();
    }
}

public class GetServiceRequestNotificationsQueryHandler(iPathDbContext db)
    : IRequestHandler<GetServiceRequestNotificationsQuery, Task<List<NotificationDto>>>
{
    public async Task<List<NotificationDto>> Handle(GetServiceRequestNotificationsQuery request, CancellationToken ct)
    {
        var query = db.NotificationQueue
            .Include(n => n.User)
            .Where(n => n.ServiceRequestId == request.ServiceRequestId);

        var notifications = await query
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