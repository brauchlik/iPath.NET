namespace iPath.Application.Features.Users;

public record SessionUserDto(Guid Id, string Username, string Email, UserGroupMemberDto[] groups, string[] roles)
{
    public static SessionUserDto Anonymous => new SessionUserDto(Guid.Empty, "", "", null, null);
}
