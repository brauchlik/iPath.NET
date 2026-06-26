namespace iPath.Domain.Entities;

public enum VsiConversionStatus
{
    Pending = 0,
    Downloading = 1,
    Converting = 2,
    Uploading = 3,
    Completed = 4,
    Failed = 5
}

public class VsiConversionJob
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public VsiConversionStatus Status { get; set; } = VsiConversionStatus.Pending;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? StartedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? OriginalStorageId { get; set; }
    public string? ConvertedStorageId { get; set; }

    public DocumentNode Document { get; set; } = null!;
}
