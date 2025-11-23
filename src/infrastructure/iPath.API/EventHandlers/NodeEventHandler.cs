using DispatchR.Abstractions.Notification;
using iPath.API.Hubs;
using iPath.Domain.Notificxations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.X509.Qualified;
using Org.BouncyCastle.Pqc.Crypto.Utilities;
using System.Diagnostics.Eventing.Reader;

namespace iPath.API;


public class DomainEventHandler(IHubContext<NodeNotificationsHub> hub,
    IUserSession sess,
    ILogger<DomainEventHandler> logger)
    : INotificationHandler<EventEntity>
{
    public async ValueTask Handle(EventEntity evt, CancellationToken ct)
    {
        if (evt is IHasNodeNotification ne)
        {
            await hub.Clients.All.SendAsync("NodeEvent", ne.ToNotification());
        }
        else 
        { 
            logger.LogWarning("Unhandled domain event {0}", evt.GetType().Name);
        }
    }

    /*
    private async ValueTask Handle(RootNodePublishedEvent evt, CancellationToken cancellationToken)
    {
        var n = new NodeNofitication(GroupId: evt.GroupId.Value, NodeId: evt.ObjectId,
            OwnerId: sess.User.Id, EventDate: DateTime.UtcNow,
            type: eNodeEventType.NodePublished, "node published");
        await hub.Clients.All.SendAsync("NodeEvent", n);
    }

    private async ValueTask Handle(AnnotationCreatedEvent evt, CancellationToken cancellationToken)
    {
        var n = new NodeNofitication(GroupId: evt.GroupId, NodeId: evt.ObjectId,
            OwnerId: sess.User.Id, EventDate: DateTime.UtcNow,
            type: eNodeEventType.NewAnnotation, "new annotation");
        await hub.Clients.All.SendAsync("NodeEvent", n);
    }
    */
}