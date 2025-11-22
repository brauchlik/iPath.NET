using DispatchR.Abstractions.Notification;
using System.Text.Json;

namespace iPath.Domain.Entities;


public interface IDomainEvent : INotification
{
    Guid EventId { get; init; }
    DateTime EventDate { get; init; }
    Guid? UserId { get; init; }
    string EventName { get; init; }
    string ObjectName { get; init; }
    Guid ObjectId { get; init; }
    string Payload { get; init; }
}


public interface IEventInput  
{
    string ObjectName { get; }
}


public class EventEntity : IDomainEvent
{
    public Guid EventId { get; init; }
    public  DateTime EventDate { get; init; }
    public Guid? UserId { get; init; }
    public string EventName { get; init; } = "";
    public string ObjectName { get; init; } = "";
    public Guid ObjectId { get; init; }
    public string Payload { get; init; } = "";


    public static TEvent Create<TEvent, TInput>(TInput input, Guid objectId, Guid? userId = null) where TEvent : IDomainEvent, new() where TInput : IEventInput
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
        return e;
    }
}



public class TestEvent : INotification
{
    public string Message { get; set; }
}