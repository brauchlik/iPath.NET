using Scalar.AspNetCore;
using System.ComponentModel;
using System.Linq.Dynamic.Core;
using iPath.Application.Features.Users;
using iPath.RazorLib.Localization;
using iPath.Application.Localization;

namespace iPath.API;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder route)
    {
        route.MapGet("admin/mailbox", 
            async ([DefaultValue(0)] int page, [DefaultValue(10)] int pagesize, IMediator mediator, CancellationToken ct)
            => {
                var res = await mediator.Send(new GetEmailsQuery { Page = page, PageSize = pagesize }, ct);
                return res;
            })
            .WithTags("Admin")
            // .RequireAuthorization()
            .Produces<PagedResult<EmailMessage>>();

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
