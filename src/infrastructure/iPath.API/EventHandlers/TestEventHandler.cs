using DispatchR.Abstractions.Notification;
using iPath.API.Hubs;
using iPath.Domain.Notifications;
using Microsoft.AspNetCore.SignalR;
using Scalar.AspNetCore;

namespace iPath.API.EventHandlers;

public class TestEventHandler(IHubContext<NodeNotificationsHub> hub, IUserSession sess) 
    : INotificationHandler<TestEvent>
{
    public async ValueTask Handle(TestEvent request, CancellationToken cancellationToken)
    {
        //var n = new NodeNofitication(NodeId: Guid.Empty, UserId: sess.User.Id, EventDate: DateTime.UtcNow, 
        //    OwnerId: sess.User.Id, GroupId: null, type: eNodeEventType.Test, message: request.Message);
        //await hub.Clients.All.SendAsync("NodeEvent", n);
    }
}