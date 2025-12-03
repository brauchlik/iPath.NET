using Scalar.AspNetCore;
using System.ComponentModel;
using System.Linq.Dynamic.Core;
using iPath.Application.Features.Users;
using iPath.RazorLib.Localization;
using iPath.Application.Localization;
using iPath.Application.Features.Notifications;

namespace iPath.API;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder route)
    {
        #region "-- Mailbox --"
        var mail = route.MapGroup("mail")
            .WithTags("Mailbox");

        mail.MapGet("list", 
            ([DefaultValue(0)] int page, [DefaultValue(10)] int pagesize, IEmailRepository repo, CancellationToken ct)
            => repo.GetPage(new PagedQuery<EmailMessage> { Page = page, PageSize = pagesize }, ct))
            .Produces<PagedResult<EmailMessage>>()
            .RequireAuthorization("Admin");

        mail.MapDelete("{id}", (string id, IEmailRepository repo, CancellationToken ct)
            => repo.Delete(Guid.Parse(id), ct))
            .RequireAuthorization("Admin");

        mail.MapDelete("all", (IEmailRepository repo, CancellationToken ct)
            => repo.DeleteAll(ct))
            .RequireAuthorization("Admin");

        mail.MapPut("read/{id}", (string id, IEmailRepository repo, CancellationToken ct)
            => repo.SetReadState(Guid.Parse(id), true, ct))
            .RequireAuthorization("Admin");

        mail.MapPut("unread/{id}", (string id, IEmailRepository repo, CancellationToken ct)
            => repo.SetReadState(Guid.Parse(id), false, ct))
            .RequireAuthorization("Admin");

        mail.MapPost("send", async (EmailDto msg, IEmailRepository repo, CancellationToken ct)
            => await repo.Create(msg.Address, msg.Subject, msg.Body, ct))
            .Produces<EmailMessage>()
            .RequireAuthorization("Admin");
        #endregion "-- Mailbox --"


        #region "-- Notifications --"
        var notify = route.MapGroup("notifications")
            .WithTags("Notifications");

        notify.MapGet("list",
            ([DefaultValue(0)] int page, [DefaultValue(10)] int pagesize, eNotificationTarget target, INotificationRepository repo, CancellationToken ct)
            => repo.GetPage(new GetNotificationsQuery { Page = page, PageSize = pagesize, Target = target }, ct))
            .Produces<PagedResult<NotificationDto>>()
            .RequireAuthorization("Admin");

        notify.MapDelete("all", (INotificationRepository repo, CancellationToken ct)
            => repo.DeleteAll(ct))
            .RequireAuthorization("Admin");
        #endregion "-- Notifications --"



        route.MapGet("admin/roles", (IMediator mediator, CancellationToken ct)
            => mediator.Send(new GetRolesQuery(), ct))
            .Produces<IEnumerable<RoleDto>>()
            .WithTags("Admin")
            .RequireAuthorization("Admin");


        route.MapGet("session", (IUserSession sess) => sess.User)
            .Produces<SessionUserDto>()
            .WithTags("Session")
            .RequireAuthorization();


        route.MapGet("translations/{lang}", (string lang, LocalizationFileService srv)
            => srv.GetTranslationData(lang))
            .Produces<TranslationData>()
            .WithTags("Localization");


        return route;
    }
}
