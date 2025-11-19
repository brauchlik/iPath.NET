namespace iPath.Domain.Entities;

public class Community : AuditableEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public DateTime CreatedOn { get; set; }

    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }


    public ICollection<CommunityGroup>? Groups { get; set; } = [];
    public ICollection<CommunityMember>? Members { get; set; } = [];

    public eCommunityVisibility Visibility { get; set; } = eCommunityVisibility.Public;
    public string? BaseUrl { get; set; }

    
    public int? ipath2_id { get; set; }
}

public class CommunityMember : BaseEntity
{

    public Guid CommunityId { get; set; }
    public Community Community { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public eMemberRole Role { get; set; }
    public bool AllGroups { get; set; }

    public int? iPath2_Id { get; set; }
}

public class CommunityGroup
{
    public Guid Id { get; set; }
    public Guid CommunityId { get; set; }
    public Community Community { get; set; } = null!;
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
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