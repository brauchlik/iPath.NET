namespace iPath.Blazor.Componenents.Admin.Users;


public class CommunityMemberModel
{
    public Guid UserId { get; private set; }
    public string? UserName { get; private set; }

    public Guid CommunityId { get; private set; }
    public string CommunityName { get; private set; }

    public eMemberRole OriginalRole { get; private set; }
    public eMemberRole Role
    {
        get => field;
        set
        {
            field = value;
            if (field == eMemberRole.None)
            {
                // if user is set to banned, remove as consultant
                IsConsultant = false;
            }
        }
    }

    public bool IsConsultant { get; set; }
    public bool IsConsultantOrig { get; private set; }

    public bool Saving { get; set; } = false;
    public bool HasChange => (IsConsultantOrig != IsConsultant) || (OriginalRole != Role);

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

    public CommunityMemberModel(CommunityMemberDto dto, string communityName, Guid userId, string? username)
    {
        CommunityId = dto.CommunityId;        
        CommunityName = communityName;
        UserId = userId;
        UserName = username;
        OriginalRole = (eMemberRole)dto.Role;
        Role = (eMemberRole)dto.Role;
        IsConsultantOrig = dto.IsConsultant;
        IsConsultant = dto.IsConsultant;
    }

    public CommunityMemberModel(Guid communityId, string communityName, Guid userId, string? username)
    {
        CommunityId = communityId;
        CommunityName = communityName;
        UserId = userId;
        UserName = username;
        OriginalRole = eMemberRole.None;
        Role = eMemberRole.None;
    }

    public CommunityMemberDto ToDto()
    {
        return new CommunityMemberDto(CommunityId: this.CommunityId, UserId: this.UserId, Role: this.Role, IsConsultant: this.IsConsultant);
    }
}