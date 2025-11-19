namespace iPath.Application.Features.Users;

public record SessionUserDto(Guid Id, string Username, string Email, UserGroupMemberDto[] groups, string[] roles);
