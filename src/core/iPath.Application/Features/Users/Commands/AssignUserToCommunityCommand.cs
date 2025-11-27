namespace iPath.Application.Features.Users;

public record AssignUserToCommunityCommand(Guid userId, Guid communityId, eMemberRole role)
    : IRequest<AssignUserToCommunityCommand, Task>;
