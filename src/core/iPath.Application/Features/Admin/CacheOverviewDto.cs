namespace iPath.Application.Features.Admin;

public class CacheOverviewDto
{
    public long TotalSize { get; set; }
    public long MaxSize { get; set; }
    public int EntryCount { get; set; }
    public int CheapCount { get; set; }
    public int ExpensiveCount { get; set; }
    public long FreeDiskBytes { get; set; }
    public string? TempPath { get; set; }
}

public class CacheSyncResult
{
    public int EntriesRemoved { get; set; }
    public int EntriesCreated { get; set; }
    public int OrphansDeleted { get; set; }
    public List<string> Details { get; set; } = [];
}
