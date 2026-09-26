using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace iPath.API;

/// <summary>
/// The app is also started by build-time tools that only need its endpoints or EF model: the
/// OpenAPI document generation (GetDocument.Insider, part of every Debug build) and dotnet ef.
/// Those runs must not touch the database, storage or background work — a fresh checkout has no
/// migrated database yet, and a failing query would fail the build.
/// </summary>
public static class BuildTimeTool
{
    public static bool IsActive =>
        Microsoft.EntityFrameworkCore.EF.IsDesignTime
        || Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider" or "ef";

    /// <summary>Removes iPath's own background services; framework services stay.</summary>
    public static void RemoveBackgroundServices(IServiceCollection services)
    {
        var own = services.Where(d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType?.Assembly.GetName().Name?.StartsWith("iPath", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var descriptor in own)
            services.Remove(descriptor);
    }
}
