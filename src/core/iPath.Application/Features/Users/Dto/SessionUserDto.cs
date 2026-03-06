namespace iPath.Application.Features.Users;

public record SessionUserDto(Guid Id, string Username, string Email, string Initials, string[] roles,
    Dictionary<Guid, eMemberRole>? communities, 
    List<UserGroupMemberDto>? groups)
    // Dictionary<Guid, eMemberRole>? groups)
{
    public static SessionUserDto Anonymous => new SessionUserDto(Guid.Empty, "", "", "", [], null, null);

    public static Guid ServerGuid => Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static SessionUserDto Server => new SessionUserDto(ServerGuid, "Server", "", "", new [] {"Admin"}, null, null);

    public bool IsAuthenticated => Id != Guid.Empty;
}
