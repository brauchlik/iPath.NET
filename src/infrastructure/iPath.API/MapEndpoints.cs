using iPath.API.Endpoints;
using iPath.API.Middleware;
using Scalar.AspNetCore;

namespace iPath.API;

public static class MapEndpoints
{
    public static IEndpointRouteBuilder MapIPathApi(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlerMiddleware>();
        app.UseResponseCompression();

        var route = app.MapGroup("api/v1");
        route.MapAdminApi()
            .MapUsersApi()
            .MapCommunitiesApi()
            .MapGroupsApi()
            .MapNodeEndpoints()
            .MapNotificationApi()
            .MapTestApi()
            .MapIPathHubs();

        // OpenAPI Documentation
        app.MapOpenApi("/openapi/v1.json");
        app.MapScalarApiReference();

        return route;
    }
}
