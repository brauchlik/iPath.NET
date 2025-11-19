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
}


public enum eMemberRole
{
    None = 0,
    User = 1,
    Guest = 2,
    Inactive = 3,
    Moderator = 4
}



[Flags]
public enum eNotification
{
    None = 0,
    NewCase = 1,
    NewAnnotation = 2,
    NewAnnotationOnMyCase = 4
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