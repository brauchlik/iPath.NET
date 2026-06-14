using iPath.API.Services.SyncImport;
using iPath.Application.Features.SyncImport;

namespace iPath.API;

public static class SyncImportEndpoints
{
    public static IEndpointRouteBuilder MapSyncImportApi(this IEndpointRouteBuilder route)
    {
        var sync = route.MapGroup("admin/sync-import")
            .WithTags("Sync Import")
            .RequireAuthorization("Admin");

        sync.MapGet("groups", async (
            SyncImportService service,
            CancellationToken ct) =>
        {
            var groups = await service.GetOldGroupSummariesAsync(ct);
            return TypedResults.Ok(groups);
        }).Produces<List<OldGroupSummary>>();

        sync.MapPost("sync", async (
            SyncStartRequest request,
            SyncImportService service,
            CancellationToken ct) =>
        {
            var count = await service.SyncGroupAsync(request.GroupId, ct);
            return TypedResults.Ok(new SyncStartResponse($"Synced {count} nodes"));
        }).Produces<SyncStartResponse>();

        return route;
    }
}
