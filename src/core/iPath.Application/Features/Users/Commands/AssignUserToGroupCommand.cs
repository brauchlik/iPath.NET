namespace iPath.Application.Features.Users;

public record AssignUserToGroupCommand(Guid userId, Guid groupId, eMemberRole role, bool isConsultant)
    : IRequest<AssignUserToGroupCommand, Task<GroupMemberDto>>;
