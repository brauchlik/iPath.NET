using iPath.Application.Features.Admin;
using iPath.Application.Querying;
using iPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetEventsQueryHandler(iPathDbContext db)
    : IRequestHandler<GetEventsQuery, Task<PagedResultList<EventDto>>>
{
    public async Task<PagedResultList<EventDto>> Handle(GetEventsQuery request, CancellationToken ct)
    {
        // When IncludeNotifications is requested, we fetch separately
        // The page will use a different endpoint to get notification counts
        var q = db.EventStore.AsNoTracking()
            .ApplyQuery(request, "EventDate DESC");

        var projection = q.Select(e => new EventDto(
            EventId: e.EventId,
            EventDate: e.EventDate,
            UserId: e.UserId,
            EventName: e.EventName,
            ObjectName: e.ObjectName,
            ObjectId: e.ObjectId,
            NotificationsCount: request.IncludeNotifications ? e.Notifications.Count() : 0
            ));

        return await projection.ToPagedResultAsync(request, ct);
    }
}

