using iPath.API.Services.Notifications;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace iPath.API;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationApi(this IEndpointRouteBuilder route)
    {
        route.MapGet("api/v1/events/stream", async (
            [FromServices] ISseConnectionManager mgr,
            [FromServices] IUserSession sess,
            [FromServices] iPathDbContext db,
            [FromQuery] string? lastEventId,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (sess.User is null)
                return Results.Unauthorized();

            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

            // Optional catch-up: emit missed events since lastEventId
            if (!string.IsNullOrEmpty(lastEventId)
                && DateTime.TryParse(lastEventId, null, DateTimeStyles.RoundtripKind, out var since))
            {
                var missed = await db.Set<EventEntity>()
                    .AsNoTracking()
                    .Where(e => e.EventDate > since)
                    .OrderBy(e => e.EventDate)
                    .ToListAsync(ct);

                foreach (var evt in missed)
                {
                    var id = evt.EventDate.ToString("o");
                    if (evt is ServiceRequestEvent srEvt)
                    {
                        var summary = new DomainEventSummary(
                            evt.EventName, evt.EventId, srEvt.ServiceRequest.Id,
                            srEvt.ServiceRequest.GroupId, evt.EventDate);
                        await mgr.SendToUserAsync(sess.User.Id, "domain-event", summary, id);
                    }
                    else
                    {
                        var hint = new SystemEventHint(evt.EventName, evt.ObjectId, "system");
                        await mgr.SendToUserAsync(sess.User.Id, "system-event", hint, id);
                    }
                }
            }

            await mgr.AddConnectionAsync(sess.User.Id, ctx.Response, ct);
            return Results.Empty;
        })
        .WithTags("Notifications")
        .RequireAuthorization();

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

        return route;
    }
}
