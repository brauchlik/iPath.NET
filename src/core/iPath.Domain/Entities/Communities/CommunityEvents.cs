namespace iPath.Domain.Entities;


public class CommunityEvent : EventEntity
{
    [JsonIgnore]
    public required Community Community { get; set; }
}



public class CommunityCreatedEvent : EventEntity;
public class CommunityUpdatedEvent : EventEntity;
public class CommunityDeletedEvent : EventEntity;

public class CommunityRenamedEvent : CommunityEvent;