using iPath.API.Hubs;

namespace iPath.API;

public static class SignalrEndpoints
{
    public static IEndpointRouteBuilder MapIPathHubs(this IEndpointRouteBuilder route)
    {
        route.MapHub<NodeNotificationsHub>("/hubs/nodes");

        return route;
    }
}
