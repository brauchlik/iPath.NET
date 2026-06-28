namespace iPath.Domain.Entities;

public enum WsiConversionStatus
{
    Pending = 0,
    Downloading = 1,
    Converting = 2,
    Uploading = 3,
    Completed = 4,
    Failed = 5
}

public class WsiConversionJob
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public WsiConversionStatus Status { get; set; } = WsiConversionStatus.Pending;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? StartedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? OriginalStorageId { get; set; }
    public string? ConvertedStorageId { get; set; }
    public string? PluginType { get; set; }

    public DocumentNode Document { get; set; } = null!;
}
