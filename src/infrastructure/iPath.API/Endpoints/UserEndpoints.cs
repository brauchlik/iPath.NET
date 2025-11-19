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

        grp.MapGet("{id}", (string id, IMediator mediator, CancellationToken ct)
            => mediator.Send(new GetUserByIdQuery(Guid.Parse(id)), ct))
            .Produces<UserDto>()
            .RequireAuthorization();

        grp.MapGet("roles", (IMediator mediator, CancellationToken ct)
            => mediator.Send(new GetRolesQuery(), ct))
            .Produces<IEnumerable<RoleDto>>()
            .RequireAuthorization();


        // Commands
        grp.MapPut("role", (UpdateUserRoleCommand cmd, IMediator mediator, CancellationToken ct)
            => mediator.Send(cmd, ct))
            .Produces<Guid>()
            .RequireAuthorization("Admin");

        return route;
    }
}
