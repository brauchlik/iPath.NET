using iPath.Application.Querying;
using Microsoft.EntityFrameworkCore.Update.Internal;
using Microsoft.OpenApi.MicrosoftExtensions;

namespace iPath.API;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupsApi(this IEndpointRouteBuilder route)
    {
        var grp = route.MapGroup("groups")
            .WithTags("Groups");

        grp.MapPost("list", async (GetGroupListQuery query, [FromServices] IGroupService srv, CancellationToken ct)
            => await srv.GetGroupListAsync(query, ct))
            .Produces<PagedResultList<GroupListDto>>()
            .RequireAuthorization();

        grp.MapGet("{id}", async (string id, [FromServices] IGroupService srv, CancellationToken ct)
            => await srv.GetGroupByIdAsync(Guid.Parse(id), ct))
            .Produces<GroupDto>()
            .RequireAuthorization();

        grp.MapPost("members", async (GetGroupMembersQuery query, [FromServices] IGroupService srv, CancellationToken ct)
            => await srv.GetGroupMembersAsync(query, ct))
            .Produces<PagedResultList<GroupMemberDto>>()
            .RequireAuthorization();


        grp.MapPut("community/assign", async (AssignGroupToCommunityCommand request, [FromServices] IGroupService srv, CancellationToken ct)
            => await srv.AssignGroupToCommunityAsync(request, ct))
            .Produces<GroupAssignedToCommunityEvent>()
            .RequireAuthorization("Admin");


        grp.MapPost("create", async (CreateGroupCommand cmd, [FromServices] IGroupService srv, CancellationToken ct)
            => await srv.CreateGroupAsync(cmd, ct))
            .Produces<GroupListDto>()
            .RequireAuthorization("Admin");

        grp.MapPut("update", async (UpdateGroupCommand cmd, [FromServices] IGroupService srv, CancellationToken ct)
            => await srv.UpdateGroupAsync(cmd, ct))
            .RequireAuthorization("Admin");

        grp.MapDelete("delete/{id}", async (Guid id, [FromServices] IGroupService srv, CancellationToken ct)
            => await srv.DeleteGroupAsync(new DeleteGroupCommand(id), ct))
            .RequireAuthorization("Admin");


        grp.MapPut("questionnaire/assign", async (AssignQuestionnaireToGroupCommand cmd, [FromServices] IGroupService srv, CancellationToken ct)
            => await srv.AssignQuestionnaireToGroupAsync(cmd, ct))
            .RequireAuthorization("Admin");

        return route;
    }
}
