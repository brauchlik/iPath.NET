using Ardalis.GuardClauses;

namespace iPath.Domain.Entities;

public class Group : AuditableEntityWithEvents
{
    public string? StorageId { get; set; }
    public string Name { get; private set; } = string.Empty;

    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }

    public DateTime CreatedOn { get; set; }

    public eGroupType GroupType { get; set; } = eGroupType.DiscussionGroup;
    public eGroupVisibility Visibility { get; set; } = eGroupVisibility.MembersOnly;

    public Guid? CommunityId { get; set; }
    public Community? Community { get; set; }

    public ICollection<CommunityGroup> ExtraCommunities { get; set; } = [];

    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];


    private List<GroupMember> _Members { get; set; } = new();
    public IReadOnlyCollection<GroupMember> Members => _Members;
    public GroupMember AddMember(Guid userId, eMemberRole role)
    {
        var m = _Members.FirstOrDefault(x => x.UserId == userId);
        if (m is null)
        {
            m = new GroupMember { Group = this, UserId = userId };
            _Members.Add(m);
        }
        m.Role = role;
        m.NotificationSettings ??= new();
        return m;
    }


    public ICollection<QuestionnaireForGroup> Quesionnaires { get; set; } = [];

    public GroupSettings Settings { get; set; } = new();

    public int? ipath2_id { get; set; }


    public Group()
    {
    }

    public static Group Create(string Name, User Owner, Community Community)
    {
        Guard.Against.NullOrEmpty(Name);
        Guard.Against.Null(Owner);
        var grp = new Group
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            Name = Name,
            Owner = Owner,
            Community = Community
        };

        return grp;
    }



    public Group AssignToCommunity(Community community)
    {
        if (community != null && !this.ExtraCommunities.Any(x => x.CommunityId == community.Id))
        {
            this.ExtraCommunities.Add(new CommunityGroup()
            {
                Group = this,
                Community = community
            });
        }

        return this;
    }
    public Group AssignToCommunity(Guid communityId)
    {
        if (!this.ExtraCommunities.Any(x => x.CommunityId == communityId))
        {
            this.ExtraCommunities.Add(new CommunityGroup()
            {
                Group = this,
                CommunityId = communityId
            });
        }

        return this;
    }

    public void RenameGroup(string newName)
    {
        Guard.Against.NullOrEmpty(newName);
        if (newName != Name)
        {
            {
                Name = newName;
                AddEventEntity(new GroupRenamedEvent { Group = this });
            }
        }
    }
}


public enum eMemberRole
{
    None = 0,
    User = 1,
    Guest = 2,
    Banned = 3,
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