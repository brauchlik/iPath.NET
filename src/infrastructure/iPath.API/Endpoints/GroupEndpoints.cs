namespace iPath.API;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupsApi(this IEndpointRouteBuilder route)
    {
        var grp = route.MapGroup("groups")
            .WithTags("Groups");

        grp.MapPost("list", (GetGroupListQuery query, IMediator mediator, CancellationToken ct)
            => mediator.Send(query, ct))
            .Produces<PagedResultList<GroupListDto>>()
            .RequireAuthorization();

        grp.MapGet("{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new GetGroupByIdQuery(Guid.Parse(id)), ct))
            .Produces<GroupDto>()
            .RequireAuthorization();


        grp.MapPut("community/assign", (AssignGroupToCommunityCommand request, IMediator mediator, CancellationToken ct)
            => mediator.Send(request, ct))
            .Produces<GroupAssignedToCommunityEvent>()
            .RequireAuthorization("Admin");


        return route;
    }
}
