namespace iPath.API.Services.Cache;

public class CacheResult
{
    public string? LocalPath { get; init; }
    public bool CanServeDirectly { get; init; }
    public string? DirectStreamPath { get; init; }
}

public record CacheStats(long TotalSize, int EntryCount, long CheapCount, long ExpensiveCount);

public interface ICacheManager
{
    Task<CacheResult> GetOrPrepareAsync(Guid documentId, DocumentNode document, Func<CancellationToken, Task<bool>> fetchFromStorage, CancellationToken ct);
    Task RecordAccessAsync(Guid documentId);
    Task EvictIfNeededAsync(long incomingSize, CancellationToken ct);
    Task RunNormalEvictionAsync(CancellationToken ct);
    Task<CacheStats> GetStatsAsync();
}
