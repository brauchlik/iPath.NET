using iPath.Application.Features.SyncImport;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.API;

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncApi(this IEndpointRouteBuilder route)
    {
        var sync = route.MapGroup("admin/sync")
            .WithTags("Sync Import")
            .RequireAuthorization("Developer")
            .DisableAntiforgery();

        sync.MapGet("groups", async (HttpContext ctx, CancellationToken ct) =>
        {
            var runner = ctx.RequestServices.GetService<ISyncImportRunner>();
            return runner is null
                ? Results.Ok(new List<OldGroupSummary>())
                : Results.Ok(await runner.GetOldGroupSummariesAsync(ct));
        })
            .Produces<List<OldGroupSummary>>();

        sync.MapGet("groups/{groupId:int}/status", async (int groupId, HttpContext ctx, CancellationToken ct) =>
        {
            var runner = ctx.RequestServices.GetService<ISyncImportRunner>();
            return runner is null
                ? Results.NotFound(new { error = "Sync import not available (ipath_old not configured)" })
                : Results.Ok(await runner.GetGroupImportStatusAsync(groupId, ct));
        })
            .Produces<GroupImportStatus>()
            .Produces(StatusCodes.Status404NotFound);

        sync.MapPost("groups/{groupId:int}", (int groupId, HttpContext ctx) =>
        {
            var jobs = ctx.RequestServices.GetService<ISyncJobManager>();
            if (jobs is null)
                return Results.BadRequest(new { error = "Sync import not available (ipath_old not configured)" });
            var userId = GetUserId(ctx);
            try
            {
                var jobId = jobs.StartSync(groupId, userId);
                return Results.Ok(new SyncStartResponse(jobId.ToString()));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
            .Produces<SyncStartResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        sync.MapPost("groups/{groupId:int}/reimport", (int groupId, HttpContext ctx) =>
        {
            var jobs = ctx.RequestServices.GetService<ISyncJobManager>();
            if (jobs is null)
                return Results.BadRequest(new { error = "Sync import not available (ipath_old not configured)" });
            var userId = GetUserId(ctx);
            try
            {
                var jobId = jobs.StartReimport(groupId, userId);
                return Results.Ok(new SyncStartResponse(jobId.ToString()));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
            .Produces<SyncStartResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        sync.MapPost("groups/{groupId:int}/delete", (int groupId, HttpContext ctx) =>
        {
            var jobs = ctx.RequestServices.GetService<ISyncJobManager>();
            if (jobs is null)
                return Results.BadRequest(new { error = "Sync import not available (ipath_old not configured)" });
            var userId = GetUserId(ctx);
            try
            {
                var jobId = jobs.StartDelete(groupId, userId);
                return Results.Ok(new SyncStartResponse(jobId.ToString()));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
            .Produces<SyncStartResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        sync.MapGet("job", (HttpContext ctx) =>
        {
            var jobs = ctx.RequestServices.GetService<ISyncJobManager>();
            var job = jobs?.Current;
            return job is null ? Results.NoContent() : Results.Ok(job);
        })
            .Produces<SyncJobState>()
            .Produces(StatusCodes.Status204NoContent);

        return route;
    }

    private static Guid? GetUserId(HttpContext ctx)
    {
        var val = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return val is not null && Guid.TryParse(val, out var id) ? id : null;
    }
}
