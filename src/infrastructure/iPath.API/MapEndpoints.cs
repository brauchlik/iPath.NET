using iPath.API.Endpoints;
using iPath.API.Middleware;
using iPath.EF.Core.FeatureHandlers;
using Microsoft.Extensions.Configuration;
using Scalar.AspNetCore;

namespace iPath.API;

public static class MapEndpoints
{
    public static IEndpointRouteBuilder MapIPathApi(this WebApplication app, IConfiguration config)
    {
        app.UseMiddleware<ExceptionHandlerMiddleware>();
        // app.UseResponseCompression();

        var route = app.MapGroup("api/v1");
        route.MapAdminApi()
            .MapUsersApi()
            .MapCommunitiesApi()
            .MapGroupsApi()
            .MapServiceRequestEndpoints()
            .MapDocumentEndpoints()
            .MapNotificationApi()
            .MapQuesionnairesApi()
            .MapFhirApi()
            .MapTestApi()
            .MapStatisticsApi()
            .MapCmsApi()
            .MapIPathHubs();

        // OpenAPI Documentation
        app.MapOpenApi("/openapi/v1.json");

        var cfg = new iPathClientConfig();
        config.GetSection(iPathClientConfig.ConfigName).Bind(cfg);

        var baseAddress = cfg.BaseAddress;
        app.MapScalarApiReference((opts, httpContext) =>
        {
            if (!string.IsNullOrEmpty(baseAddress))
            {
                opts.Servers = [];
                opts.Servers.Add(new ScalarServer(baseAddress, ""));
                opts.BaseServerUrl = baseAddress;
            }

            opts.WithTitle($"API for {httpContext.User.Identity?.Name}");
            opts.PreserveSchemaPropertyOrder();
        });

        return route;
    }
}
