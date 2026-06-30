namespace iPath.Domain.Entities.Cache;

public enum eFileCost { Cheap, Expensive }
public enum eCacheState { Cached, Extracting, Failed }

public class DocumentCacheEntry
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public eFileCost Cost { get; set; }
    public eCacheState State { get; set; }
    public DateTime LastAccessed { get; set; }
    public int AccessCount { get; set; }
    public DateTime CreatedOn { get; set; }
}
