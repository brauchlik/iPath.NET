namespace iPath.Application.Contracts;

public interface IUserSession
{
    SessionUserDto? User { get; }
    void ReloadUser(Guid userId);
}


public static class UserSessionExtensions
{
    extension(IUserSession session)
    {
        public bool IsAuthenticated => session.User is not null && session.User.Id != Guid.Empty;
        public bool IsAdmin => session.User is not null && session.User.roles.Any(r => r.ToLower() == "admin");
        public bool IsModerator => session.User is not null && session.User.roles.Any(r => r.ToLower() == "moderator");


        /// <summary>
        /// Check if user has access to the GroupId and throw an NotAllowedException if not
        /// </summary>
        /// <param name="GroupId"></param>
        /// <exception cref="NotAllowedException"></exception>
        public void AssertInGroup(Guid? GroupId)
        {
            if (!session.IsAdmin)
            {
                if (!GroupId.HasValue || !session.User.groups.ContainsKey(GroupId.Value))
                {
                    throw new NotAllowedException($"You are not allowed to access group {GroupId}");
                }
            }
        }

        public HashSet<Guid> GroupIds() => !session.User.IsAuthenticated ? [] : session.User.groups.Keys.ToHashSet();

        public void AssertInRole(string Role)
        {
            if (!session.User.roles.Any(r => r.ToLower() == Role.ToLower()))
            {
                throw new NotAllowedException();
            }
        }


        public bool IsGroupModerator(Guid? groupId, bool AllowAdminAsModerator = true)
        {
            if (groupId.HasValue && session.IsAuthenticated)
            {
                if (AllowAdminAsModerator && session.IsAdmin)
                    return true;
                if (session.User.groups.ContainsKey(groupId.Value) && session.User.groups[groupId.Value] == eMemberRole.Moderator)
                    return true;
            }
            return false;
        }

        // Admin or user himself
        public bool CanModifyUser(Guid UserId)
            => session.IsAuthenticated && (session.IsAdmin || UserId == session.User.Id);


        // Admin or user himself
        public bool IsOwner(ServiceRequestDto dto)
            => dto is not null && session.IsAuthenticated && dto.OwnerId == session.User.Id;


        public string Username => session.Username;

        public bool CanEditNode(ServiceRequestDto? node)
        {
            if (session.User is null || node is null )
                return false;

            if (session.IsAdmin)
                return true;

            if (session.IsGroupModerator(node.GroupId))
                return true;

            if (node.OwnerId == session.User.Id)
                return true;

            return false;
        }
    }
}