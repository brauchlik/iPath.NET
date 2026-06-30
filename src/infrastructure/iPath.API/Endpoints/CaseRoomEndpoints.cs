using iPath.Application.Features.CaseRoom;

namespace iPath.API;

public static class CaseRoomEndpoints
{
    public static IEndpointRouteBuilder MapCaseRoomApi(this IEndpointRouteBuilder route)
    {
        var group = route.MapGroup("caseroom");

        group.MapPost("{requestId:guid}/join", async (
            Guid requestId,
            SessionRequest body,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            [FromQuery] string? token,
            CancellationToken ct) =>
        {
            bool isGuest = false;
            Guid userId;
            string username;

            if (sess.User is null || !sess.User.IsAuthenticated)
            {
                if (string.IsNullOrEmpty(token) || !await store.IsShareTokenValidAsync(requestId, token, ct))
                    return Results.Unauthorized();

                isGuest = true;
                userId = Guid.Empty;
                username = "Guest";
            }
            else
            {
                userId = sess.User.Id;
                username = sess.User.Username;
            }

            var snapshot = await store.JoinAsync(requestId, body.SessionId, userId, username, isGuest, body.InitialDocumentId, body.InitialIsWSI, body.InitialFilename, ct);
            return Results.Ok(snapshot);
        });

        group.MapPost("{requestId:guid}/leave", async (
            Guid requestId,
            SessionRequest body,
            [FromServices] ICaseRoomSessionStore store,
            CancellationToken ct) =>
        {
            await store.LeaveAsync(requestId, body.SessionId, ct);
            return Results.NoContent();
        });

        group.MapPost("{requestId:guid}/sync", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            SyncPayload payload,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (sess.User is null || (!sess.User.IsAuthenticated && !ctx.User.IsInRole("CaseRoomGuest")))
                return Results.Unauthorized();

            if (payload.Viewport is not null && !payload.Viewport.IsValid())
                return Results.BadRequest("Invalid viewport coordinates");

            await store.SyncAsync(requestId, payload.SessionId ?? Guid.Empty, sess.User.Id, payload, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("{requestId:guid}", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            CancellationToken ct) =>
        {
            var status = await store.GetStatusAsync(requestId, ct);
            return status is null ? Results.NotFound() : Results.Ok(status);
        });

        group.MapPost("{requestId:guid}/share-token", async (
            Guid requestId,
            SessionRequest body,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            var token = await store.CreateShareTokenAsync(requestId, body.InitialDocumentId, body.InitialIsWSI, body.InitialFilename, ct);
            return Results.Ok(new { token });
        }).RequireAuthorization();

        return route;
    }
}
