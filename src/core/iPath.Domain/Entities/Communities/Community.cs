using Ardalis.GuardClauses;

namespace iPath.Domain.Entities;

public class Community : AuditableEntity
{
    public string Name { get; set; } = "";

    public DateTime CreatedOn { get; set; }

    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }

    public CommunitySettings Settings { get; set; } = new();

    public ICollection<Group>? Groups { get; set; } = [];


    private List<CommunityGroup> _ExtraGroups { get; set; } = new();
    public IReadOnlyCollection<CommunityGroup> ExtraGroups => _ExtraGroups;
    public CommunityGroup AddExtraGroup(Group group)
    {
        var ret = _ExtraGroups.FirstOrDefault(g => g.GroupId == group.Id);
        if (ret is null)
        {
            ret = new CommunityGroup { Community = this, Group = group };
            _ExtraGroups.Add(ret);
        }
        return ret;
    }
    public void RemoveExtraGroup(Group group)
    {
        _ExtraGroups.RemoveAll(m => m.GroupId == group.Id);
    }


    private List<CommunityMember> _Members { get; set; } = new();
    public IReadOnlyCollection<CommunityMember> Members => _Members;

    public CommunityMember AddMember(User user, eMemberRole? role = null)
    {
        var ret = Members.FirstOrDefault(m => m.UserId == user.Id);
        if (ret is null)
        {
            ret = new CommunityMember { User = user, Community = this };
            _Members.Add(ret);
        };
        if (role.HasValue)
        {
            ret.Role = role.Value;
        }
        return ret;
    }
    public void RemoveMember(User user)
    {
        _Members.RemoveAll(m => m.UserId == user.Id);
    }


    public eCommunityVisibility Visibility { get; set; } = eCommunityVisibility.Public;


    public ICollection<QuestionnaireForCommunity> Quesionnaires { get; set; } = [];


    public int? ipath2_id { get; set; }

    public Community()
    {   
    }

    public static Community Create(string Name, User Owner)
    {
        Guard.Against.NullOrEmpty(Name);
        Guard.Against.Null(Owner);
        return new Community
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            Name = Name,
            Owner = Owner,  
        };
    }

    public static Community Create(string Name, Guid OwnerId)
    {
        Guard.Against.NullOrEmpty(Name);
        return new Community
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            Name = Name,
            OwnerId = OwnerId,
        };
    }
}


public enum eCommunityVisibility
{
    Public = 0,
    MembersOnly = 1,
    Hidden = 2,
    Inactive = 3
}



public class CommunityCreatedEvent : EventEntity;
public class CommunityUpdatedEvent : EventEntity;
public class CommunityDeletedEvent : EventEntity;