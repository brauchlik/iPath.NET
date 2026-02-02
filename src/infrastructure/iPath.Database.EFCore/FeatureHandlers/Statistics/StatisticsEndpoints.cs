using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace iPath.API;

public static class StatisticsEndpoints
{
    public static IEndpointRouteBuilder MapStatisticsApi(this IEndpointRouteBuilder route)
    {
        var stats = route.MapGroup("stats")
                .WithTags("Statistics")
                .RequireAuthorization();


        stats.MapGet("documents", async (iPathDbContext db, CancellationToken ct) =>
        {
            return await db.Documents.AsNoTracking()
                .GroupBy(x => x.DocumentType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync();
        })
            .RequireAuthorization("Admin");


        return route;
    }
}
