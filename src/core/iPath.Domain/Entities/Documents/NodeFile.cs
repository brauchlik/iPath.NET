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

    public long? FileSize { get; set; }

    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }

    public DocumentConversionStatus? ConversionStatus { get; set; }

    public bool ConversionSkipped { get; set; }

    public int ThumbRetryCount { get; set; }

    /// <summary>Where the file is read from.</summary>
    public StorageInfo? Storage { get; set; }

    /// <summary>
    /// Earlier locations left behind by a storage migration; deleted by the migration's purge.
    /// </summary>
    // Files stored before this list existed have no such JSON property and load it as null. The
    // field name deliberately avoids EF's backing-field convention, so EF goes through the accessors.
    public List<StorageInfo> RetiredLocations
    {
        get => retired ??= [];
        set => retired = value ?? [];
    }
    private List<StorageInfo>? retired;

    public NodeFile Clone()
    {
        var clone = (NodeFile)MemberwiseClone();
        clone.RetiredLocations = [.. RetiredLocations];
        return clone;
    }
}



