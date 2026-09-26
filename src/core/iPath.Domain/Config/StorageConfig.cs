namespace iPath.Domain.Config;

/// <summary>
/// Named storage instances. Without any instance configured, a single "LocalFiles" instance at
/// <see cref="iPathConfig.LocalDataPath"/> is used, which is the behaviour before named instances.
/// </summary>
public class StorageConfig
{
    public const string ConfigName = "Storage";

    /// <summary>Instance used for new files; must name an entry of <see cref="Instances"/>.</summary>
    public string? Default { get; set; }

    public Dictionary<string, StorageInstanceConfig> Instances { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public enum StorageInstanceType
{
    LocalFiles,
    S3,
}

public class StorageInstanceConfig
{
    public StorageInstanceType Type { get; set; }

    /// <summary>LocalFiles: root folder.</summary>
    public string? Path { get; set; }

    /// <summary>S3: endpoint, e.g. http://localhost:9000 for RustFS / MinIO; empty for AWS.</summary>
    public string? ServiceUrl { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string Region { get; set; } = "us-east-1";

    /// <summary>S3: path-style addressing, required by RustFS / MinIO.</summary>
    public bool ForcePathStyle { get; set; } = true;
}
