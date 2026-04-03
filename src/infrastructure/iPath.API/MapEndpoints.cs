using System.IO;
using iPath.API.Endpoints;
using iPath.API.Middleware;
using iPath.EF.Core.FeatureHandlers;
using iPath.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            .MapGoogleProxy()
            .MapEmailImportApi()
            .MapIPathHubs();

        // OpenAPI Documentation
        var openapi = config.GetValue<bool>("OpenApi");
        if (openapi)
        {
            var cfg = new iPathClientConfig();
            config.GetSection(iPathClientConfig.ConfigName).Bind(cfg);

            // Use static OpenAPI file generated at build time (wwwroot/openapi/openapi.json)
            // No need for MapOpenApi() since we're using the static file
            app.MapScalarApiReference((opts, httpContext) =>
            {
                opts.WithOpenApiRoutePattern("/openapi/openapi.json");

                if (!string.IsNullOrEmpty(cfg.BaseAddress))
                {
                    opts.Servers = [];
                    opts.Servers.Add(new ScalarServer(cfg.BaseAddress, ""));
                    opts.BaseServerUrl = cfg.BaseAddress;
                }

                opts.WithTitle($"API for {httpContext.User.Identity?.Name}");
                opts.PreserveSchemaPropertyOrder();
            });
        }

        return route;
    }
}
