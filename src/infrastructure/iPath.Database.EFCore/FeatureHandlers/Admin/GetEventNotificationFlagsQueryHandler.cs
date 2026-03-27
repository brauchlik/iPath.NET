using DispatchR.Abstractions.Send;
using iPath.Application.Features.Admin;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetEventNotificationFlagsQueryHandler(iPathDbContext db)
    : IRequestHandler<GetEventNotificationFlagsQuery, Task<Dictionary<Guid, int>>>
{
    public async Task<Dictionary<Guid, int>> Handle(GetEventNotificationFlagsQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EventIds))
        {
            return new Dictionary<Guid, int>();
        }

        var eventIds = request.EventIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        if (!eventIds.Any())
        {
            return new Dictionary<Guid, int>();
        }

        return await db.NotificationQueue
            .Where(n => n.EventId != null && eventIds.Contains(n.EventId!.Value))
            .GroupBy(n => n.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId!.Value, x => x.Count, ct);
    }
}