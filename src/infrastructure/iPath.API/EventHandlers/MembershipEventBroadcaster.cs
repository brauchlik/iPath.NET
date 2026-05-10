using DispatchR.Abstractions.Notification;
using iPath.API.Services.Notifications;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;

namespace iPath.API.EventHandlers;

public class MembershipEventBroadcaster(ISseConnectionManager sse, ILogger<MembershipEventBroadcaster> logger)
    : INotificationHandler<EventEntity>
{
    public async ValueTask Handle(EventEntity evt, CancellationToken ct)
    {
        if (evt is not ServiceRequestEvent srEvt) return;

        var groupId = srEvt.ServiceRequest.GroupId;
        var summary = new DomainEventSummary(
            evt.EventName,
            evt.EventId,
            srEvt.ServiceRequest.Id,
            groupId,
            evt.EventDate);

        await sse.SendToGroupMembersAsync(groupId, "domain-event", summary, evt.EventDate.ToString("o"));
        logger.LogDebug("Broadcast domain-event {EventName} for group {GroupId}", evt.EventName, groupId);
    }
}
