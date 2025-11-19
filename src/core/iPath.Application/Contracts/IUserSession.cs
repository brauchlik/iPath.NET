namespace iPath.Application.Contracts;

public interface IUserSession
{
    SessionUserDto? User { get; }
}

public static class UserSessionExtensions
{
    public static HashSet<Guid> GroupIds(this IUserSession session)
    {
        if (session.User is null || session.User.groups is null)
        {
            return [];
        }
        return session.User.groups.Select(g => g.GroupId).ToHashSet();
    }

    public static void AssertInGroup(this IUserSession session, Guid GroupId)
    {
        if (!session.IsAdmin())
        {
            if (!session.User.groups.Any(g => g.GroupId == GroupId))
            {
                throw new NotAllowedException();
            }
        }
    }

    public static void AssertInRole(this IUserSession session, string Role)
    {
        if (!session.User.roles.Any(r => r.ToLower() == Role.ToLower()))
        {
            throw new NotAllowedException();
        }
    }

    public static bool IsAdmin(this IUserSession session)
    {
        return session.User.roles.Any(r => r.ToLower() == "admin");
    }
}