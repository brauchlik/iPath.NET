using iPath.Application.Querying;
using Microsoft.OpenApi.MicrosoftExtensions;

namespace iPath.API;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupsApi(this IEndpointRouteBuilder route)
    {
        var grp = route.MapGroup("groups")
            .WithTags("Groups");

        grp.MapPost("list", async (GetGroupListQuery query, IGroupService srv, CancellationToken ct)
            => await srv.GetGroupListAsync(query, ct))
            .Produces<PagedResultList<GroupListDto>>()
            .RequireAuthorization();

        grp.MapGet("{id}", async (string id, IGroupService srv, CancellationToken ct)
            => await srv.GetGroupByIdAsync(Guid.Parse(id), ct))
            .Produces<GroupDto>()
            .RequireAuthorization();

        grp.MapPost("members", async (GetGroupMembersQuery query, IGroupService srv, CancellationToken ct)
            => await srv.GetGroupMembersAsync(query, ct))
            .Produces<PagedResultList<GroupMemberDto>>()
            .RequireAuthorization();


        grp.MapPut("community/assign", async (AssignGroupToCommunityCommand request, IGroupService srv, CancellationToken ct)
            => await srv.AssignGroupToCommunityAsync(request, ct))
            .Produces<GroupAssignedToCommunityEvent>()
            .RequireAuthorization("Admin");


        grp.MapPost("create", async (CreateGroupCommand cmd, IGroupService srv, CancellationToken ct)
            => await srv.CreateGroupAsync(cmd, ct))
            .Produces<GroupListDto>()
            .RequireAuthorization("Admin");

        grp.MapPut("update", async (UpdateGroupCommand cmd, IGroupService srv, CancellationToken ct)
            => await srv.UpdateGroupAsync(cmd, ct))
            .RequireAuthorization("Admin");

        grp.MapDelete("delete/{id}", async (Guid id, IGroupService srv, CancellationToken ct)
            => await srv.DeleteGroupAsync(new DeleteGroupCommand(id), ct))
            .RequireAuthorization("Admin");


        return route;
    }
}
