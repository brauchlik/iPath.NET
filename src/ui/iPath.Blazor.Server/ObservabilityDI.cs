using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace iPath.Blazor.Server;

public static class ObservabilityDI
{
    public static ILoggingBuilder AddOTLPLogging(this ILoggingBuilder logging)
    {
        logging.AddOpenTelemetry(opts =>
        {
            opts.IncludeScopes = true;
            opts.IncludeFormattedMessage = true;
        });
        return logging;
    }

    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("iPath.NET"))
            .WithTracing(cfg => cfg.AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddEntityFrameworkCoreInstrumentation())
            .WithMetrics(cfg => cfg.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .UseOtlpExporter();

        return services;
    }
}
