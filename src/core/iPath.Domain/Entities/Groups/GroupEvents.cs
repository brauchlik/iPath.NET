namespace iPath.Domain.Entities;


public class GroupEvent : EventEntity
{
    [JsonIgnore]
    public required Group Group { get; set; }
}


public class GroupRenamedEvent : GroupEvent;
