using iPath.Domain.Config;
using iPath.Domain.Entities.Cache;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Cache;

public class CacheManager(
    iPathDbContext db,
    IOptions<iPathConfig> ipathOpts,
    IOptions<CacheSettings> settings,
    ILogger<CacheManager> logger)
    : ICacheManager
{
    public async Task<CacheResult> GetOrPrepareAsync(Guid documentId, DocumentNode document, Func<CancellationToken, Task<bool>> fetchFromStorage, CancellationToken ct)
    {
        var cost = ClassifyCost(document);
        var tempPath = Path.Combine(ipathOpts.Value.TempDataPath, document.Id.ToString());

        var entry = await db.DocumentCacheEntries.FirstOrDefaultAsync(e => e.DocumentId == documentId, ct);
        if (entry is not null)
        {
            entry.LastAccessed = DateTime.UtcNow;
            entry.AccessCount++;
            await db.SaveChangesAsync(ct);
            return new CacheResult { LocalPath = entry.FilePath };
        }

        // Check if provider can serve directly (LocalStorage cheap files)
        if (CanServeDirectly(document) && cost == eFileCost.Cheap)
        {
            var directPath = ResolveDirectPath(document);
            if (directPath is not null && File.Exists(directPath))
            {
                await CreateEntryAsync(documentId, document, tempPath, cost, eCacheState.Cached);
                return new CacheResult { DirectStreamPath = directPath, CanServeDirectly = true };
            }
        }

        // Need to download from storage
        var downloaded = await fetchFromStorage(ct);
        if (!downloaded || !File.Exists(tempPath))
            return new CacheResult();

        var state = cost == eFileCost.Expensive ? eCacheState.Extracting : eCacheState.Cached;
        await CreateEntryAsync(documentId, document, tempPath, cost, state);

        // Evict proactively if needed
        await EvictIfNeededAsync(0, ct);

        return new CacheResult { LocalPath = tempPath };
    }

    public async Task RecordAccessAsync(Guid documentId)
    {
        var entry = await db.DocumentCacheEntries.FirstOrDefaultAsync(e => e.DocumentId == documentId);
        if (entry is not null)
        {
            entry.LastAccessed = DateTime.UtcNow;
            entry.AccessCount++;
            await db.SaveChangesAsync();
        }
    }

    public async Task EvictIfNeededAsync(long incomingSize, CancellationToken ct)
    {
        var total = await db.DocumentCacheEntries.SumAsync(e => (long?)e.FileSize ?? 0, ct);
        var maxBytes = settings.Value.MaxCacheSizeBytes;
        if (total + incomingSize <= maxBytes) return;

        var targetFree = Math.Max(incomingSize * 2, (long)((total + incomingSize - maxBytes) * 1.2));
        var hotCutoff = DateTime.UtcNow.AddMinutes(-5);

        var candidates = await db.DocumentCacheEntries
            .Where(e => e.LastAccessed < hotCutoff)
            .ToListAsync(ct);

        candidates = candidates
            .OrderByDescending(e => (DateTime.UtcNow - e.LastAccessed).TotalDays * e.FileSize)
            .ToList();

        long freed = 0;
        foreach (var entry in candidates)
        {
            if (freed >= targetFree) break;
            TryDeletePath(entry.FilePath);
            db.DocumentCacheEntries.Remove(entry);
            freed += entry.FileSize;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Pressure eviction freed {FreedBytes} MB ({TargetFree} MB target)", freed / 1024 / 1024, targetFree / 1024 / 1024);
    }

    public async Task RunNormalEvictionAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cheapCutoff = now - settings.Value.CheapRetentionSpan;
        var expensiveCutoff = now - settings.Value.ExpensiveRetentionSpan;

        var delete = await db.DocumentCacheEntries
            .Where(e => (e.Cost == eFileCost.Cheap && e.LastAccessed < cheapCutoff)
                     || (e.Cost == eFileCost.Expensive && e.LastAccessed < expensiveCutoff))
            .ToListAsync(ct);

        foreach (var entry in delete)
        {
            TryDeletePath(entry.FilePath);
            db.DocumentCacheEntries.Remove(entry);
        }

        await db.SaveChangesAsync(ct);
        if (delete.Count > 0)
            logger.LogInformation("Normal eviction removed {Count} expired cache entries", delete.Count);
    }

    public async Task<CacheStats> GetStatsAsync()
    {
        var entries = await db.DocumentCacheEntries.ToListAsync();
        return new CacheStats(
            TotalSize: entries.Sum(e => e.FileSize),
            EntryCount: entries.Count,
            CheapCount: entries.Count(e => e.Cost == eFileCost.Cheap),
            ExpensiveCount: entries.Count(e => e.Cost == eFileCost.Expensive)
        );
    }

    private static eFileCost ClassifyCost(DocumentNode document)
    {
        if (document.DocumentType == "wsi") return eFileCost.Expensive;
        var ext = Path.GetExtension(document.File?.Filename ?? "");
        if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase)
         || string.Equals(ext, ".dzi", StringComparison.OrdinalIgnoreCase)
         || string.Equals(ext, ".vsi", StringComparison.OrdinalIgnoreCase))
            return eFileCost.Expensive;
        return eFileCost.Cheap;
    }

    private static bool CanServeDirectly(DocumentNode document)
    {
        var storage = document.File?.Storage;
        return storage is not null && storage.ProviderName == "LocalFiles" && !string.IsNullOrEmpty(storage.StorageId);
    }

    private static string? ResolveDirectPath(DocumentNode document)
    {
        var storage = document.File?.Storage;
        if (storage is null || storage.ProviderName != "LocalFiles") return null;
        // Path: {LocalDataPath}/{GroupId}/{ServiceRequestId}/{StorageId}
        // We need the config for LocalDataPath, but we don't have it here directly
        return null; // Resolved at call site where config is available
    }

    private async Task CreateEntryAsync(Guid documentId, DocumentNode document, string filePath, eFileCost cost, eCacheState state)
    {
        var fi = new FileInfo(filePath);
        var entry = new DocumentCacheEntry
        {
            Id = Guid.CreateVersion7(),
            DocumentId = documentId,
            StorageProvider = document.File?.Storage?.ProviderName ?? "",
            FilePath = filePath,
            FileSize = fi.Exists ? fi.Length : 0,
            Cost = cost,
            State = state,
            LastAccessed = DateTime.UtcNow,
            AccessCount = 1,
            CreatedOn = DateTime.UtcNow
        };
        db.DocumentCacheEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    private static void TryDeletePath(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            else if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}
