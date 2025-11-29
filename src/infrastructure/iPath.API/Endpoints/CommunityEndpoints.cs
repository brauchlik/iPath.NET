using iPath.Application.Features.Users;
using Microsoft.AspNetCore.Mvc;

namespace iPath.API;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunitiesApi(this IEndpointRouteBuilder route)
    {
        var grp = route.MapGroup("communities")
            .WithTags("Communities");

        grp.MapPost("list", async (GetCommunityListQuery query, IMediator mediator, CancellationToken ct)
            => await mediator.Send(query, ct))
            .Produces<PagedResultList<CommunityListDto>>();

        grp.MapGet("{id}", async (string id, IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetCommunityByIdQuery(Guid.Parse(id)), ct))
            .Produces<CommunityDto>()
            .RequireAuthorization();

        grp.MapPost("members", async (string id, IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetCommunityMembersQuery { CommunityId = Guid.Parse(id) }, ct))
            .Produces<PagedResultList<CommunityMemberDto>>()
            .RequireAuthorization();


        grp.MapPost("create", async ([FromBody] CreateCommunityCommand request, IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<CommunityListDto>()
            .RequireAuthorization();

        grp.MapPut("update", async ([FromBody] UpdateCommunityCommand request, IMediator mediator, CancellationToken ct)
            => await mediator.Send(request, ct))
            .Produces<CommunityListDto>()
            .RequireAuthorization();

        grp.MapDelete("{id}", async (string id, IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteCommunityCommand(Guid.Parse(id)), ct))
            .Produces<Guid>()
            .RequireAuthorization();

        return route;
    }
}
