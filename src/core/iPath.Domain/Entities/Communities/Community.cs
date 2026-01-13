using Ardalis.GuardClauses;

namespace iPath.Domain.Entities;

public class Community : AuditableEntity
{
    public string Name { get; set; } = "";

    public DateTime CreatedOn { get; set; }

    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }

    public CommunitySettings Settings { get; set; } = new();

    public ICollection<CommunityGroup>? Groups { get; set; } = [];
    public ICollection<CommunityMember>? Members { get; set; } = [];

    public eCommunityVisibility Visibility { get; set; } = eCommunityVisibility.Public;

    
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