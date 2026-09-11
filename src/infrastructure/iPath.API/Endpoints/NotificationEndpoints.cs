using iPath.API.Services.Notifications;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace iPath.API;

public static class NotificationEndpoints
{
    private static readonly TimeSpan MaxReplayWindow = TimeSpan.FromHours(24);
    private const int MaxReplayEvents = 200;

    public static IEndpointRouteBuilder MapNotificationApi(this IEndpointRouteBuilder route)
    {
        route.MapGet("events/stream", async (
            [FromServices] ISseConnectionManager mgr,
            [FromServices] IUserSession sess,
            [FromServices] iPathDbContext db,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (sess.User is null || (!sess.User.IsAuthenticated && !ctx.User.IsInRole("CaseRoomGuest")))
                return Results.Unauthorized();

            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

            var lastEventId = ctx.Request.Query["lastEventId"].FirstOrDefault()
                           ?? ctx.Request.Headers["Last-Event-ID"].FirstOrDefault();

            // Replay events missed while disconnected. Guests get none: they share a
            // synthetic principal, so "the user's groups" is meaningless for them.
            if (!string.IsNullOrEmpty(lastEventId)
                && sess.User.IsAuthenticated
                && DateTime.TryParse(lastEventId, null, DateTimeStyles.RoundtripKind, out var since))
            {
                // Bound the replay: a client reconnecting after a long absence must not
                // pull an unbounded slice of the event table into memory.
                if (since < DateTime.UtcNow - MaxReplayWindow)
                    since = DateTime.UtcNow - MaxReplayWindow;

                var myGroupIds = await db.Set<GroupMember>()
                    .AsNoTracking()
                    .Where(m => m.UserId == sess.User.Id && m.Role != eMemberRole.Banned)
                    .Select(m => m.GroupId)
                    .Distinct()
                    .ToListAsync(ct);

                // Projected, not Include()d: ServiceRequest is a plain navigation property and
                // lazy loading is off, so dereferencing it on a tracked-free entity throws.
                var missedRequests = await db.Set<ServiceRequestEvent>()
                    .AsNoTracking()
                    .Where(e => e.EventDate > since && myGroupIds.Contains(e.ServiceRequest.GroupId))
                    .OrderBy(e => e.EventDate)
                    .Take(MaxReplayEvents)
                    .Select(e => new
                    {
                        e.EventName,
                        e.EventId,
                        RequestId = e.ServiceRequest.Id,
                        GroupId = e.ServiceRequest.GroupId,
                        e.EventDate
                    })
                    .ToListAsync(ct);

                foreach (var e in missedRequests)
                {
                    var summary = new DomainEventSummary(e.EventName, e.EventId, e.RequestId, e.GroupId, e.EventDate);
                    await mgr.SendToUserAsync(sess.User.Id, "domain-event", summary, e.EventDate.ToString("o"));
                }

                var missedSystem = await db.Set<EventEntity>()
                    .AsNoTracking()
                    .Where(e => e.EventDate > since && !(e is ServiceRequestEvent))
                    .OrderBy(e => e.EventDate)
                    .Take(MaxReplayEvents)
                    .Select(e => new { e.EventName, e.ObjectId, e.EventDate })
                    .ToListAsync(ct);

                foreach (var e in missedSystem)
                {
                    var hint = new SystemEventHint(e.EventName, e.ObjectId, "system");
                    await mgr.SendToUserAsync(sess.User.Id, "system-event", hint, e.EventDate.ToString("o"));
                }
            }

            await mgr.AddConnectionAsync(sess.User.Id, ctx.Response, ct);
            return Results.Empty;
        })
        .WithTags("Notifications");

        route.MapPost("notifications/{id:guid}/read", async (
            Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new MarkNotificationAsReadCommand(id), ct);
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithTags("Notifications")
        .RequireAuthorization();

        route.MapPost("notifications/read-all", async (
            [FromServices] IUserSession sess,
            [FromServices] INotificationRepository repo,
            CancellationToken ct) =>
        {
            if (sess.User is null)
                return Results.Unauthorized();
            await repo.MarkAllAsRead(sess.User.Id, ct);
            return Results.NoContent();
        })
        .WithTags("Notifications")
        .RequireAuthorization();

        route.MapDelete("notifications/{id:guid}", async (Guid id, HttpContext ctx, [FromServices] INotificationRepository repo, [FromServices] IUserSession sess, CancellationToken ct) =>
        {
            if (sess.User is null) return Results.Unauthorized();
            await repo.Delete(id, sess.User.Id, ct);
            return Results.Ok();
        })
        .WithTags("Notifications")
        .RequireAuthorization();

        route.MapGet("notifications/unread-count", async (
            [FromServices] IUserSession sess,
            [FromServices] INotificationRepository repo,
            CancellationToken ct) =>
        {
            if (sess.User is null)
                return Results.Unauthorized();
            var count = await repo.GetUnreadCount(sess.User.Id, eNotificationTarget.InApp, ct);
            return Results.Ok(count);
        })
        .WithTags("Notifications")
        .RequireAuthorization();

        return route;
    }
}
