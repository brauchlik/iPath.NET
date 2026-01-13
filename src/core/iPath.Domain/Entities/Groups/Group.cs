using Ardalis.GuardClauses;

namespace iPath.Domain.Entities;

public class Group : AuditableEntity
{
    public string? StorageId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }

    public DateTime CreatedOn { get; set; }

    public eGroupType GroupType { get; set; } = eGroupType.DiscussionGroup;
    public eGroupVisibility Visibility { get; set; } = eGroupVisibility.MembersOnly;

    public ICollection<CommunityGroup> Communities { get; set; } = [];

    public ICollection<Node> Nodes { get; set; } = [];
    public ICollection<GroupMember> Members { get; set; } = [];

    public ICollection<QuestionnaireForGroup> Quesionnaires { get; set; } = [];

    public GroupSettings Settings { get; set; } = new();
    
    public int? ipath2_id { get; set; }


    public Group()
    {   
    }

    public static Group Create(string Name, User Owner, Community? DefaultCommunity = null)
    {
        Guard.Against.NullOrEmpty(Name);
        Guard.Against.Null(Owner);
        var grp = new Group
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            Name = Name,
            Owner = Owner,
        };

        if (DefaultCommunity is not null)
        {
            grp.AssignToCommunity(DefaultCommunity);
        }

        return grp;
    }



    public Group AssignToCommunity(Community community)
    {
        if (community != null && !this.Communities.Any(x => x.CommunityId == community.Id))
        {
            this.Communities.Add(new CommunityGroup()
            {
                Group = this,
                Community = community
            });
        }

        return this;
    }
}


public enum eMemberRole
{
    None = 0,
    User = 1,
    Guest = 2,
    Inactive = 3,
    Moderator = 4
}



public enum eGroupVisibility
{
    Public = 0,
    MembersOnly = 1,
    Hidden = 2,
    Inactive = 3
}

public enum eGroupType
{
    None = 0,
    DiscussionGroup = 1,
    ExpertGroup = 2,
    TeachingGroup = 3,
    PresentationGroup = 4
}