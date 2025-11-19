namespace iPath.Application.Features.Users;

public record GetRolesQuery() : IRequest<GetRolesQuery, Task<IEnumerable<RoleDto>>>;
