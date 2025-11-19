using System.Text.Json;

namespace iPath.EF.Core.FeatureHandlers.EventStore;

public static class EventStoreExtensions
{
    public static async Task<TEvent> CreateEventAsync<TEvent, TInput>(this iPathDbContext db, 
        TInput input, 
        Guid objectId, 
        Guid? userId = null,
        CancellationToken ct= default) 
        where TEvent : EventEntity, new() where TInput : IEventInput
    {
        var e = new TEvent
        {
            EventId = Guid.CreateVersion7(),
            EventDate = DateTime.UtcNow,
            UserId = userId,
            EventName = typeof(TEvent).Name,
            ObjectName = input.ObjectName,
            ObjectId = objectId,
            Payload = JsonSerializer.Serialize(input)
        };
        await db.EventStore.AddAsync(e, ct);
        return e;
    }
}