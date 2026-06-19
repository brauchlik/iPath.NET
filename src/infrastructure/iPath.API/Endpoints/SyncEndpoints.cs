using iPath.Application.Features.SyncImport;

namespace iPath.API;

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncApi(this IEndpointRouteBuilder route)
    {
        var sync = route.MapGroup("admin/sync")
            .WithTags("Sync Import")
            .RequireAuthorization("Developer");

        sync.MapGet("groups", async ([FromServices] ISyncImportRunner runner, CancellationToken ct)
            => await runner.GetOldGroupSummariesAsync(ct))
            .Produces<List<OldGroupSummary>>();

        sync.MapGet("groups/{groupId:int}/status", async (int groupId, [FromServices] ISyncImportRunner runner, CancellationToken ct)
            => await runner.GetGroupImportStatusAsync(groupId, ct))
            .Produces<GroupImportStatus>();

        sync.MapPost("groups/{groupId:int}", (int groupId, [FromServices] ISyncJobManager jobs) =>
        {
            try
            {
                var jobId = jobs.StartSync(groupId);
                return Results.Ok(new SyncStartResponse(jobId.ToString()));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
            .Produces<SyncStartResponse>()
            .Produces(StatusCodes.Status409Conflict);

        sync.MapPost("groups/{groupId:int}/reimport", (int groupId, [FromServices] ISyncJobManager jobs) =>
        {
            try
            {
                var jobId = jobs.StartReimport(groupId);
                return Results.Ok(new SyncStartResponse(jobId.ToString()));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
            .Produces<SyncStartResponse>()
            .Produces(StatusCodes.Status409Conflict);

        sync.MapGet("job", ([FromServices] ISyncJobManager jobs) =>
        {
            var job = jobs.Current;
            return job is null ? Results.NoContent() : Results.Ok(job);
        })
            .Produces<SyncJobState>()
            .Produces(StatusCodes.Status204NoContent);

        return route;
    }
}
