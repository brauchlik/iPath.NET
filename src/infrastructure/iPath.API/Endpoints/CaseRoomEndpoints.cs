using iPath.Application.Features.CaseRoom;

namespace iPath.API;

public static class CaseRoomEndpoints
{
    public static IEndpointRouteBuilder MapCaseRoomApi(this IEndpointRouteBuilder route)
    {
        var group = route.MapGroup("caseroom").RequireAuthorization();

        group.MapPost("{requestId:guid}/join", async (
            Guid requestId,
            SessionRequest body,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            var snapshot = await store.JoinAsync(requestId, body.SessionId, sess.User.Id, sess.User.Username, ct);
            return Results.Ok(snapshot);
        });

        group.MapPost("{requestId:guid}/leave", async (
            Guid requestId,
            SessionRequest body,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            await store.LeaveAsync(requestId, body.SessionId, ct);
            return Results.NoContent();
        });

        group.MapPost("{requestId:guid}/sync", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            SyncPayload payload,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            if (payload.Viewport is not null && !payload.Viewport.IsValid())
                return Results.BadRequest("Invalid viewport coordinates");

            await store.SyncAsync(requestId, payload.SessionId ?? Guid.Empty, sess.User.Id, payload, ct);
            return Results.NoContent();
        });

        group.MapGet("{requestId:guid}", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            CancellationToken ct) =>
        {
            var status = await store.GetStatusAsync(requestId, ct);
            return status is null ? Results.NotFound() : Results.Ok(status);
        });

        return route;
    }
}
