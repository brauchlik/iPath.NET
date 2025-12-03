using iPath.Domain.Notificxations;
using System.Text.Json;

namespace iPath.Domain.Entities;

public class Notification : BaseEntity
{
    public DateTime CreatedOn { get; private set; }
    public DateTime? ProcessedOn { get; private set; }
    public NotificationStatus Status { get; private set; } = NotificationStatus.Pending;

    public Guid UserId {  get; private set; }
    public User User { get; private set; }

    public eNodeEventType EventType { get; private set; } = eNodeEventType.None;
    public eNotificationTarget Target { get; private set; } = eNotificationTarget.None;
    public bool DailySummary { get; private set; }

    public string Data { get; private set; }
    public string? ErrorMessage { get; private set; }


    private Notification()
    {        
    }



    public static Notification Create(eNodeEventType type, eNotificationTarget target, bool dailySummary, string data)
    {
        return new Notification
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            EventType = type,
            Target = target,
            DailySummary = dailySummary,
            Status = NotificationStatus.Pending,
            Data = data
        };
    }

    public static Notification Create(eNodeEventType type, eNotificationTarget target, bool dailySummary, NodeNofitication data)
    {
        var options = new JsonSerializerOptions
        {
            IncludeFields = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var context = new NodeNofiticationSerializerContext(options);
        var json = JsonSerializer.Serialize(data, typeof(NodeNofitication), context);

        return new Notification
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            EventType = type,
            Target = target,
            DailySummary = dailySummary,
            Status = NotificationStatus.Pending,
            Data = json
        };
    }

    public Notification MarkAsSent()
    {
        Status = NotificationStatus.Sent;
        ProcessedOn = DateTime.UtcNow;
        return this;
    }

    public Notification MarkAsFailed(string errorMessage)
    {
        Status = NotificationStatus.Failed;
        ProcessedOn = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        return this;
    }

    public Notification UpdateStatus(NotificationStatus status)
    {
        Status = status;
        return this;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(NodeNofitication))]
internal partial class NodeNofiticationSerializerContext : JsonSerializerContext
{
}


[Flags]
public enum eNotificationTarget
{
    None = 0,
    InApp = 1,
    Email = 2
}



public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}


