using Microsoft.AspNetCore.Mvc;

namespace iPath.API;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunitiesApi(this IEndpointRouteBuilder route)
    {
        var grp = route.MapGroup("communities")
            .WithTags("Communities");

        grp.MapPost("list", (GetCommunityListQuery query, IMediator mediator, CancellationToken ct)
            => mediator.Send(query, ct))
            .Produces<PagedResultList<CommunityListDto>>();

        grp.MapGet("{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new GetCommunityByIdQuery(Guid.Parse(id)), ct))
            .Produces<CommunityDto>()
            .RequireAuthorization();



        grp.MapPost("create", ([FromBody] CreateCommunityInput request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<CommunityListDto>()
            .RequireAuthorization();

        grp.MapPut("update", ([FromBody] UpdateCommunityInput request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<CommunityListDto>()
            .RequireAuthorization();

        grp.MapDelete("{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new DeleteCommunityInput(Guid.Parse(id)), ct))
            .Produces<Guid>()
            .RequireAuthorization();

        return route;
    }
}
