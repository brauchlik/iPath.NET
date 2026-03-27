namespace iPath.Application.Features.Admin;

public class EventWithNotificationsDto
{
    public Guid EventId { get; init; }
    public DateTime EventDate { get; init; }
    public Guid UserId { get; init; }
    public string EventName { get; init; } = "";
    public string ObjectName { get; init; } = "";
    public Guid ObjectId { get; init; }
    public string Payload { get; init; } = "";
    
    // Extension
    public int NotificationCount { get; init; }
}