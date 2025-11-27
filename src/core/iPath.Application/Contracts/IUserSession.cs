using System.Collections.ObjectModel;

namespace iPath.Application.Contracts;

public interface IUserSession
{
    SessionUserDto? User { get; }
}

public static class UserSessionExtensions
{
    extension(IUserSession session)
    {
        public bool IsAuthenticated => session.User is not null;
        public bool IsAdmin => session.User.roles.Any(r => r.ToLower() == "admin");
        public bool IsModerator => session.User.roles.Any(r => r.ToLower() == "moderator");


        public void AssertInGroup(Guid GroupId)
        {
            if (!session.IsAdmin)
            {
                if (!session.User.groups.ContainsKey(GroupId))
                {
                    throw new NotAllowedException();
                }
            }
        }

        public HashSet<Guid> GroupIds() => session.User.groups.Keys.ToHashSet();

        public void AssertInRole(string Role)
        {
            if (!session.User.roles.Any(r => r.ToLower() == Role.ToLower()))
            {
                throw new NotAllowedException();
            }
        }


        public bool IsGroupModerator(Guid groupId)
            => session.IsAuthenticated && session.User.groups.ContainsKey(groupId) && session.User.groups[groupId] == eMemberRole.Moderator;

        // Admin or user himself
        public bool CanModifyUser(Guid UserId)
            => session.IsAuthenticated && (session.IsAdmin || UserId == session.User.Id);


        public string Username => session.Username;

        public bool CanEditNode(NodeDto? node)
        {
            if (session.User is null || node is null )
                return false;

            if (session.IsAdmin)
                return true;

            if (node.GroupId.HasValue && session.IsGroupModerator(node.GroupId.Value))
                return true;

            if (node.OwnerId == session.User.Id)
                return true;

            return false;
        }
    }
}