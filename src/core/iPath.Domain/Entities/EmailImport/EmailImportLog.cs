namespace iPath.Domain.Entities;

public class EmailImportLog : BaseEntity
{
    public string MailboxName { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public string? Subject { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ServiceRequestId { get; set; }
    public EmailImportStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedOn { get; set; }
}

public enum EmailImportStatus
{
    Pending = 0,
    Imported = 1,
    Skipped = 2,
    Quarantined = 3,
    Failed = 4,
    Deleted = 5
}