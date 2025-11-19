namespace iPath.Domain.Entities.Mails;

public class Notification : BaseEntity
{
    public DateTime CreatedOn { get; private set; }
    public DateTime? ProcessedOn { get; private set; }
    public NotificationStatus Status { get; private set; } = NotificationStatus.Pending;

    public Guid UserId {  get; private set; }
    public User User { get; private set; }

    public NotificationType Type { get; private set; } = NotificationType.None;

    public string Data { get; private set; }
    public string? ErrorMessage { get; private set; }

    private Notification()
    {        
    }

    public static Notification Create(NotificationType type, string data)
    {
        return new Notification
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            Type = type,
            Status = NotificationStatus.Pending,
            Data = data
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


public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public enum NotificationType
{
    None = 0,
    InApp = 1,
    Email = 2
}