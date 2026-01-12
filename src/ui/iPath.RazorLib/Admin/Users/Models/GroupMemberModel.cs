using System.ComponentModel;
using System.Diagnostics;

namespace iPath.Blazor.Componenents.Admin.Users;


[DebuggerDisplay("{Username} - {GroupName} - {Role}")]
public class GroupMemberModel : INotifyPropertyChanged
{
    public Guid UserId { get; private set; }
    public string Username { get; private set; }
    public Guid GroupId { get; private set; }
    public string GroupName { get; private set; }
    public eMemberRole OriginalRole { get; private set; }
    public eMemberRole Role { get; set; }


    public event PropertyChangedEventHandler? PropertyChanged;
    public bool Saving { get; set; } = false;
    public bool HasChange => OriginalRole != Role;

    private void NotifyRoleChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUser)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModerator)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGuest)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBanned)));
    }

    public bool IsModerator
    {
        get => Role == eMemberRole.Moderator;
        set {
            Role = value ? eMemberRole.Moderator : ToggleRole(Role);
            NotifyRoleChanged();
        }
    }
    public bool IsUser
    {
        get => Role == eMemberRole.User;
        set
        {
            Role = value ? eMemberRole.User : ToggleRole(Role);
            NotifyRoleChanged();
        }
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

    public GroupMemberModel(Guid groupId, Guid userId, string username)
    {
        GroupId = groupId;
        UserId  = userId;
        Username = username;
        OriginalRole = eMemberRole.None;
        Role = eMemberRole.None;
    }

    public GroupMemberModel(GroupMemberDto m, Guid groupId, string groupName)
    {
        GroupId = groupId;
        GroupName = groupName;
        UserId = m.UserId;
        Username = m.Username;
        Role = m.Role;
        OriginalRole = m.Role;
    }

    public UserGroupMemberDto ToDto()
    {
        return new UserGroupMemberDto(GroupId: this.GroupId, Role: this.Role, Groupname: this.GroupName);
    }

    public GroupMemberDto ToGroupMemberDto()
    {
           return new GroupMemberDto(UserId: this.UserId, Username: this.Username, Role: this.Role);
    }
}