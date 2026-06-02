using DispatchR.Abstractions.Notification;
using iPath.API.Services.Notifications;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;

namespace iPath.API.EventHandlers;

public class SystemEventBroadcaster(
    ISseConnectionManager sse,
    INotificationEventBus eventBus,
    ILogger<SystemEventBroadcaster> logger)
    : INotificationHandler<EventEntity>
{
    public async ValueTask Handle(EventEntity evt, CancellationToken ct)
    {
        if (evt is ServiceRequestEvent) return;

        var hint = new SystemEventHint(evt.EventName, evt.ObjectId, DeriveHint(evt));
        await sse.BroadcastAsync("system-event", hint, evt.EventDate.ToString("o"));
        eventBus.PublishSystemEvent(hint);
        logger.LogDebug("Broadcast system-event {EventName}", evt.EventName);
    }

    private static string DeriveHint(EventEntity evt) => evt.EventName switch
    {
        var n when n.Contains("Group") => "group",
        var n when n.Contains("Community") => "community",
        var n when n.Contains("User") => "user",
        _ => "system"
    };
}
