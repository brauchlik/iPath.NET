namespace iPath.Blazor.Componenents.Admin.Users;


public class GroupMemberModel
{
    public Guid GroupId { get; private set; }
    public string GroupName { get; private set; }
    public eMemberRole OriginalRole { get; private set; }
    public eMemberRole Role { get; set; }
    public bool HasChange => OriginalRole != Role;

    public bool IsModerator
    {
        get => Role == eMemberRole.Moderator;
        set => Role = value ? eMemberRole.Moderator : ToggleRole(Role);
    }
    public bool IsUser
    {
        get => Role == eMemberRole.User;
        set => Role = value ? eMemberRole.User : ToggleRole(Role);
    }
    public bool IsGuest
    {
        get => Role == eMemberRole.Guest;
        set => Role = value ? eMemberRole.Guest : ToggleRole(Role);
    }
    public bool IsBanned
    {
        get => Role == eMemberRole.Inactive;
        set => Role = value ? eMemberRole.Inactive : ToggleRole(Role);
    }
    private eMemberRole ToggleRole(eMemberRole input)
    {
        return Role == input ? eMemberRole.None : Role;
    }

    public GroupMemberModel(UserGroupMemberDto dto, string groupName)
    {
        GroupId = dto.GroupId;
        GroupName = groupName;
        OriginalRole = (eMemberRole)dto.Role;
        Role = (eMemberRole)dto.Role;
    }

    public GroupMemberModel(Guid groupId, string groupName)
    {
        GroupId = groupId;
        GroupName = groupName;
        OriginalRole = eMemberRole.None;
        Role = eMemberRole.None;
    }

    public UserGroupMemberDto ToDto()
    {
        return new UserGroupMemberDto(GroupId: this.GroupId, Role: this.Role, Groupname: null);
    }
}