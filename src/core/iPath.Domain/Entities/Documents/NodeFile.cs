using System.Text.Json.Serialization;

namespace iPath.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentConversionStatus
{
    Pending,
    Converting,
    Completed,
    Failed
}

public class NodeFile
{
    public DateTime? LastStorageExportDate { get; set; }
    public string? Filename { get; set; }
    public string? MimeType { get; set; }
    public string? ThumbData { get; set; }

    // No longer read or written; removed together with the StorageLocation migration so this
    // change does not need a model migration of its own.
    public string? PublicUrl { get; set; }

    public long? FileSize { get; set; }

    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }

    public DocumentConversionStatus? ConversionStatus { get; set; }

    public bool ConversionSkipped { get; set; }

    public int ThumbRetryCount { get; set; }

    public StorageInfo? Storage { get; set; }

    public NodeFile Clone() => (NodeFile)MemberwiseClone();
}



