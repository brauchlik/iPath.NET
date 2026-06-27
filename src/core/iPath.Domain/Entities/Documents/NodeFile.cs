namespace iPath.Domain.Entities;

public class NodeFile
{
    public DateTime? LastStorageExportDate { get; set; }
    public string? Filename { get; set; }
    public string? MimeType { get; set; }
    public string? ThumbData { get; set; }

    public string? PublicUrl { get; set; }

    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }

    public string? ConversionStatus { get; set; }

    public int ThumbRetryCount { get; set; }

    public StorageInfo? Storage { get; set; }

    public NodeFile Clone() => (NodeFile)MemberwiseClone();
}



