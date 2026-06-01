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

        if (srEvt.ServiceRequest is null)
        {
            logger.LogWarning("ServiceRequestEvent {EventId} has null ServiceRequest navigation property; skipping broadcast", srEvt.EventId);
            return;
        }

        var groupId = srEvt.ServiceRequest.GroupId;
        var summary = new DomainEventSummary(
            srEvt.EventName,
            srEvt.EventId,
            srEvt.ServiceRequest.Id,
            groupId,
            srEvt.EventDate);

        await sse.SendToGroupMembersAsync(groupId, "domain-event", summary, srEvt.EventDate.ToString("o"));
        logger.LogDebug("Broadcast domain-event {EventName} for group {GroupId}", srEvt.EventName, groupId);
    }
}
