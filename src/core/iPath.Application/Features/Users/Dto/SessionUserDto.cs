namespace iPath.Application.Features.Users;

public record SessionUserDto(Guid Id, string Username, string Email, string Initials, string[] roles, Dictionary<Guid, eMemberRole>? communities, Dictionary<Guid, eMemberRole>? groups)
{
    public static SessionUserDto Anonymous => new SessionUserDto(Guid.Empty, "", "", "", [], null, null);
}
