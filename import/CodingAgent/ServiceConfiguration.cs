using CodingAgent.Features.Coding;
using DispatchR.Extensions;
using iPath.API;
using iPath.Application.Coding;
using iPath.Application.Fhir;
using iPath.Domain.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodingAgent;

public static class ServiceConfiguration
{
    public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddLogging();
        services.Configure<iPathConfig>(config.GetSection(iPathConfig.ConfigName));

        services.AddDispatchR(cfg => cfg.Assemblies.Add(typeof(iPath.Application.Meta).Assembly));
        services.AddPersistance(config);

        services.AddScoped<IFhirDataLoader, FileFhirDataLoader>();
        services.AddScoped<CodingService>(sp =>
        {
            return new CodingService(sp, "icdo");
        });
        services.AddScoped<CodingPlugin>();
        services.AddScoped<BodySiteCoding>();

        return services;
    }
}
