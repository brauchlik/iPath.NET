using DispatchR;
using iPath.Application.Features.Users;
using iPath.Application.Features.Users.Commands;
using Scalar.AspNetCore;

namespace iPath.API;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUsersApi(this IEndpointRouteBuilder route)
    {
        var grp = route.MapGroup("users")
            .WithTags("Users");

        // Queries
        grp.MapPost("list", async (GetUserListQuery query, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(query, ct))
            .Produces<PagedResultList<UserListDto>>()
            .RequireAuthorization();

        grp.MapGet("{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetUserByIdQuery(Guid.Parse(id)), ct))
            .Produces<UserDto>()
            .RequireAuthorization();

        // Commands
        grp.MapPost("create", async (CreateUserCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<OwnerDto>()
            .RequireAuthorization("Admin");

        grp.MapPut("role", async (UpdateUserRoleCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization("Admin");

        grp.MapPut("account", async (UpdateUserAccountCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");

        grp.MapPut("password", async (UpdateUserPasswordCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");

        grp.MapPut("profile", async (UpdateUserProfileCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization();

        grp.MapDelete("{id}", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteUserCommand(Guid.Parse(id)), ct))
            .RequireAuthorization("Admin");


        // communities
        grp.MapPut("communities", async (UpdateCommunityMembershipCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization("Admin");

        grp.MapPut("assign/community", async (AssignUserToCommunityCommand cmd, [FromServices] IMediator mediator , CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");


        // groups
        grp.MapPut("assign/group", async (AssignUserToGroupCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");

        grp.MapPut("groups", async (UpdateGroupMembershipCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization("Admin");


        // notifications
        grp.MapGet("{id}/notifications", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetUserNotificationsQuery(Guid.Parse(id)), ct))
            .RequireAuthorization();            
        
        grp.MapPost("notifications", async (UpdateUserNotificationsCommand cmd, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization();


        // upload folders
        grp.MapPost("{id}/uploadfolder", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new CreateRequestUploadFolderCommand(Guid.Parse(id)), ct))
            .RequireAuthorization("Admin");

        grp.MapDelete("{id}/uploadfolder", async (string id, [FromServices] IMediator mediator, CancellationToken ct)
            => await mediator.Send(new DeleteUserUploadFolderCommand(Guid.Parse(id)), ct))
            .RequireAuthorization("Admin");

        return route;
    }
}
