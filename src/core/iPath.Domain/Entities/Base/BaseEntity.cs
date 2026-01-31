namespace iPath.Domain.Entities;


public interface IEntity
{
}

public interface IBaseEntity : IEntity
{
    Guid Id { get; set; }
}

public class BaseEntity : IBaseEntity
{
    public Guid Id { get; set; }
}

public class AuditableEntity : BaseEntity
{
    public DateTime CreatedOn { get; set; }
    public DateTime? DeletedOn { get; set; }
    public DateTime? LastModifiedOn { get; set; }
}


public interface ISoftDelete
{
    DateTime? DeletedOn { get; set; }
}


public class AuditableEntityWithEvents : AuditableEntity, IHasDomainEvents, ISoftDelete
{
    [JsonIgnore]
    public List<EventEntity> Events { get; set; } = new();

    public void AddEventEntity(EventEntity eventItem) 
        => Events.Add(eventItem);

    public void ClearDomainEvents()
        => Events.Clear();
}


public interface IHasDomainEvents : IBaseEntity
{
    public List<EventEntity> Events { get; }
    public void AddEventEntity(EventEntity eventItem);
    public void ClearDomainEvents();
}