using iPath.EF.Core.Database;
using System.Text.Json;

namespace iPath.EF.Core.FeatureHandlers.EventStore;

public static class EventStoreExtensions
{
    public static async Task<TEvent> CreateEventAsync<TEvent, TInput>(this iPathDbContext db, 
        TInput input, 
        Guid objectId,
        IUserSession sess,
        CancellationToken ct= default) 
        where TEvent : EventEntity, new() where TInput : IEventInput
    {
        var e = new TEvent
        {
            EventId = Guid.CreateVersion7(),
            EventDate = DateTime.UtcNow,
            UserId = sess.User.Id,
            EventName = typeof(TEvent).Name,
            ObjectName = input.ObjectName,
            ObjectId = objectId,
            Payload = JsonSerializer.Serialize(input)
        };
        await db.EventStore.AddAsync(e, ct);
        return e;
    }


    public static async Task<TEvent> CreateEventAsync<TEvent, TInput, TEntity>(this iPathDbContext db,
        TInput input,
        TEntity entity,
        IUserSession sess,
        CancellationToken ct = default)
        where TEvent : EventEntity, new() 
        where TInput : IEventInput
        where TEntity : class, IHasDomainEvents
    {
        var e = new TEvent
        {
            EventId = Guid.CreateVersion7(),
            EventDate = DateTime.UtcNow,
            UserId = sess.User.Id,
            EventName = typeof(TEvent).Name,
            ObjectName = input.ObjectName,
            ObjectId = entity.Id,
            Payload = JsonSerializer.Serialize(input)
        };
        await db.EventStore.AddAsync(e, ct);
        entity.AddEventEntity(e);
        // db.Set<TEntity>().Update(entity);
        return e;
    }
}