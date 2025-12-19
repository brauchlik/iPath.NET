using iPath.Application.Features.Users;
using Scalar.AspNetCore;

namespace iPath.API;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUsersApi(this IEndpointRouteBuilder route)
    {
        var grp = route.MapGroup("users")
            .WithTags("Users");

        // Queries
        grp.MapPost("list", async (GetUserListQuery query, IMediator mediator, CancellationToken ct)
            => await mediator.Send(query, ct))
            .Produces<PagedResultList<UserListDto>>()
            .RequireAuthorization();

        grp.MapGet("{id}", async (string id, IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetUserByIdQuery(Guid.Parse(id)), ct))
            .Produces<UserDto>()
            .RequireAuthorization();

        // Commands
        grp.MapPost("create", async (CreateUserCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<OwnerDto>()
            .RequireAuthorization("Admin");

        grp.MapPut("role", async (UpdateUserRoleCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization("Admin");

        grp.MapPut("account", async (UpdateUserAccountCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");

        grp.MapPut("password", async (UpdateUserPasswordCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");

        grp.MapPut("profile", async (UpdateUserProfileCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization();


        // communities
        grp.MapPut("communities", async (UpdateCommunityMembershipCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization("Admin");

        grp.MapPut("assign/community", async (AssignUserToCommunityCommand cmd, IMediator mediator , CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");


        // groups
        grp.MapPut("assign/group", async (AssignUserToGroupCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");

        grp.MapPut("groups", async (UpdateGroupMembershipCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization("Admin");


        // notifications
        grp.MapGet("{id}/notifications", async (string id, IMediator mediator, CancellationToken ct)
            => await mediator.Send(new GetUserNotificationsQuery(Guid.Parse(id)), ct))
            .RequireAuthorization();            
        
        grp.MapPost("notifications", async (UpdateUserNotificationsCommand cmd, IMediator mediator, CancellationToken ct)
            => await mediator.Send(cmd, ct))
            .RequireAuthorization();


        return route;
    }
}
