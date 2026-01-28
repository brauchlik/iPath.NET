using iPath.Application.Features.CMS;
using Scalar.AspNetCore;

namespace iPath.API;

public static class CmsEndpoints
{
    public static IEndpointRouteBuilder MapCmsApi(this IEndpointRouteBuilder route)
    {
        var grp = route.MapGroup("cms")
            .WithTags("Web Content");


        grp.MapPost("list", async (GetWebContentsQuery query, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(query, ct))
            .Produces<PagedResultList<WebContentDto>>();


        grp.MapPost("create", async (CreateWebContentCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<WebContentDto>()
            .RequireAuthorization("Admin");

        grp.MapPut("{id}", async (string id, UpdateWebContentCommand request, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<WebContentDto>()
            .RequireAuthorization("Admin");

        grp.MapDelete("{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteWebContentCommand(Guid.Parse(id)), ct))
            .RequireAuthorization("Admin");

        return route;
    }
}

