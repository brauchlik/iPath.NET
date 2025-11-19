namespace iPath.Application.Features.Users;

public record UpdateUserRoleCommand(Guid UserId, Guid RoleId, bool allow) 
    : IRequest<UpdateUserRoleCommand, Task<Guid>>;
