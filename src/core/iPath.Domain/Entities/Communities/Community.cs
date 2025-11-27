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