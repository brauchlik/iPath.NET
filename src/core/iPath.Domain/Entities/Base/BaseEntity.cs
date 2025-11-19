namespace iPath.Domain.Entities;


public interface IEntity
{
}

public interface IBaseEntity
{
    Guid Id { get; set; }
}

public class BaseEntity : IBaseEntity
{
    public Guid Id { get; set; }
}

public class AuditableEntity : BaseEntity
{
    public DateTime? DeletedOn { get; set; }
    public DateTime? LastModifiedOn { get; set; }
}


public interface IHasDomainEvents
{
    public List<EventEntity> Events { get; }
    public void AddDomainEvent(EventEntity eventItem);
    public void ClearDomainEvents();
}