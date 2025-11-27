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
        grp.MapPost("list", (GetUserListQuery query, IMediator mediator, CancellationToken ct)
            => mediator.Send(query, ct))
            .Produces<PagedResultList<UserListDto>>()
            .RequireAuthorization();

        grp.MapGet("{id}", async (string id, IMediator mediator, CancellationToken ct)
            => {
                var u = await mediator.Send(new GetUserByIdQuery(Guid.Parse(id)), ct);
                return u;
            })
            .Produces<UserDto>()
            .RequireAuthorization();

        // Commands
        grp.MapPost("create", (CreateUserCommand cmd, IMediator mediator, CancellationToken ct)
            => mediator.Send(cmd, ct))
            .Produces<OwnerDto>()
            .RequireAuthorization("Admin");

        grp.MapPut("role", (UpdateUserRoleCommand cmd, IMediator mediator, CancellationToken ct)
            => mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization("Admin");

        grp.MapPut("account", (UpdateUserAccountCommand cmd, IMediator mediator, CancellationToken ct)
            => mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");


        grp.MapPut("groups", (UpdateGroupMembershipCommand cmd, IMediator mediator, CancellationToken ct)
            => mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization("Admin");

        grp.MapPut("assign/community", (AssignUserToCommunityCommand cmd, IMediator mediator , CancellationToken ct)
            => mediator.Send(cmd, ct))
            .RequireAuthorization("Admin");


        grp.MapPut("profile", (UpdateUserProfileCommand cmd, IMediator mediator, CancellationToken ct)
            => mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization();

        return route;
    }
}
