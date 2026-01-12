namespace iPath.Application.Features.Users;

public record AssignUserToGroupCommand(Guid userId, Guid groupId, eMemberRole role)
    : IRequest<AssignUserToGroupCommand, Task<GroupMemberDto>>;
