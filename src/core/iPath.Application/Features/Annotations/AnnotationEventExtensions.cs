using System.Text.Json;

namespace iPath.Application.Features.Annotations;

public static class AnnotationEventExtensions
{
    public static EventEntity CreateEvent<TEvent, TInput>(this Annotation a,
        TInput input,
        Guid userId,
        CancellationToken ct = default)
        where TEvent : AnnotationEvent, new()
        where TInput : IEventInput
    {
        var e = new TEvent
        {
            EventId = Guid.CreateVersion7(),
            EventDate = DateTime.UtcNow,
            UserId = userId,
            EventName = typeof(TEvent).Name,
            ObjectName = input.ObjectName,
            ObjectId = a.Id,
            Payload = JsonSerializer.Serialize(input),
            Annotation = a,
        };
        a.LastModifiedOn = DateTime.UtcNow;
        a.AddEventEntity(e);
        return e;
    }
}
