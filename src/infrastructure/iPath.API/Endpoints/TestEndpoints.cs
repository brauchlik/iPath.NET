using DispatchR.Abstractions.Notification;
using iPath.API.Hubs;
using iPath.Domain.Notificxations;
using Microsoft.AspNetCore.SignalR;
using Scalar.AspNetCore;

namespace iPath.API;

public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestApi(this IEndpointRouteBuilder route)
    {
        route.MapPost("test/notify",
            async (TestEvent evt, IMediator mediator, CancellationToken ct)
                => mediator.Publish(evt, ct))
                .WithTags("Test")
                .RequireAuthorization();

        return route;
    }
}



public class TestEventHandler(IHubContext<NodeNotificationsHub> hub, IUserSession sess) 
    : INotificationHandler<TestEvent>
{
    public async ValueTask Handle(TestEvent request, CancellationToken cancellationToken)
    {
        var n = new NodeNofitication(Guid.Empty, Guid.Empty, sess.User.Id, 
            DateTime.UtcNow, eNodeEventType.Test, request.Message);
        await hub.Clients.All.SendAsync("NodeEvent", n);
    }
}