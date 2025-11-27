namespace iPath.API;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupsApi(this IEndpointRouteBuilder route)
    {
        var grp = route.MapGroup("groups")
            .WithTags("Groups");

        grp.MapPost("list", (GetGroupListQuery query, IGroupService srv, CancellationToken ct)
            => srv.GetGroupListAsync(query, ct))
            .Produces<PagedResultList<GroupListDto>>()
            .RequireAuthorization();

        grp.MapGet("{id}", (string id, IGroupService srv, CancellationToken ct)
            => srv.GetGroupByIdAsync(Guid.Parse(id), ct))
            .Produces<GroupDto>()
            .RequireAuthorization();


        grp.MapPut("community/assign", (AssignGroupToCommunityCommand request, IGroupService srv, CancellationToken ct)
            => srv.AssignGroupToCommunityAsync(request, ct))
            .Produces<GroupAssignedToCommunityEvent>()
            .RequireAuthorization("Admin");


        grp.MapPost("create", async (CreateGroupCommand cmd, IGroupService srv, CancellationToken ct)
            => await srv.CreateGroupAsync(cmd, ct))
            .Produces<GroupListDto>()
            .RequireAuthorization("Admin");

        grp.MapPut("update", (UpdateGroupCommand cmd, IGroupService srv, CancellationToken ct)
            => srv.UpdateGroupAsync(cmd, ct))
            .RequireAuthorization("Admin");

        grp.MapDelete("delete/{id}", (Guid id, IGroupService srv, CancellationToken ct)
            => srv.DeleteGroupAsync(new DeleteGroupCommand(id), ct))
            .RequireAuthorization("Admin");


        return route;
    }
}
