namespace iPath.Application.Features.Users;

public record AssignUserToCommunityCommand(Guid userId, Guid communityId, eMemberRole role, bool isConsultant)
    : IRequest<AssignUserToCommunityCommand, Task>;
