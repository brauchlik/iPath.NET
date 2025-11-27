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

        grp.MapGet("members/{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new GetCommunityMembersQuery(Guid.Parse(id)), ct))
            .Produces<CommunityDto>()
            .RequireAuthorization();


        grp.MapPost("create", ([FromBody] CreateCommunityCommand request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<CommunityListDto>()
            .RequireAuthorization();

        grp.MapPut("update", ([FromBody] UpdateCommunityCommand request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<CommunityListDto>()
            .RequireAuthorization();

        grp.MapDelete("{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new DeleteCommunityCommand(Guid.Parse(id)), ct))
            .Produces<Guid>()
            .RequireAuthorization();

        return route;
    }
}
