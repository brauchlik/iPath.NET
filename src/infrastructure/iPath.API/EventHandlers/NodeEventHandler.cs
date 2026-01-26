using DispatchR.Abstractions.Notification;
using iPath.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace iPath.API;



// This is an experimental SignalR Hub
// NodeEvents are directly passed to the SignalR Client
// This has to be refactored and attached to the NotificatoinQueue for InApp Notifications

public class DomainEventSignalrProcessor(IHubContext<NodeNotificationsHub> hub,
    IUserSession sess,
    ILogger<DomainEventSignalrProcessor> logger)
    : INotificationHandler<EventEntity>
{
    public async ValueTask Handle(EventEntity evt, CancellationToken ct)
    {
        if (evt is ServiceRequestEvent ne)
        {
            // publish directly to SignalR Hub

            // TODO: this should be filtered over the INotificationQueue and then sent according to users "InApp" subscriptions

            string payload = JsonSerializer.Serialize(ne);
            var msg = new NotificationMessage(evt.EventDate, ne.GetType().Name, payload);
            await hub.Clients.All.SendAsync("NodeEvent", msg);
        }
        else 
        { 
            logger.LogWarning("Unhandled domain event {0}", evt.GetType().Name);
        }
    }
}

public record NotificationMessage(DateTime Date, string eventType, string payload);