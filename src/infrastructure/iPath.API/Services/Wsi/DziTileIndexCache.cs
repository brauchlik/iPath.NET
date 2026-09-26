using System.Collections.Concurrent;
using iPath.Application.Features.Conversion.Dzi;
using Microsoft.Extensions.Caching.Memory;

namespace iPath.API.Services.Wsi;

/// <summary>
/// Keeps the tile index of recently viewed DZI zips in memory, so a tile request is an index
/// lookup plus one range read. Built lazily from the zip on first access.
/// </summary>
public sealed class DziTileIndexCache(IMemoryCache cache, ILogger<DziTileIndexCache> logger)
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(20);

    // A viewer opening a slide fires many tile requests at once; only one of them builds the index.
    private readonly ConcurrentDictionary<string, Lazy<Task<DziTileIndex?>>> _building = new();

    /// <returns>The index, or null when the file is not an indexable DZI zip.</returns>
    public async Task<DziTileIndex?> GetAsync(string zipPath, CancellationToken ct)
    {
        var file = new FileInfo(zipPath);
        if (!file.Exists)
            return null;

        // Length and write time in the key: a replaced file (e.g. repacked) gets a fresh index.
        var key = $"dzi-index:{file.FullName}:{file.Length}:{file.LastWriteTimeUtc.Ticks}";
        if (cache.TryGetValue(key, out DziTileIndex? cached))
            return cached;

        var lazy = _building.GetOrAdd(key, _ => new Lazy<Task<DziTileIndex?>>(() => Task.Run(() => Build(file.FullName), CancellationToken.None)));
        try
        {
            var index = await lazy.Value.WaitAsync(ct);
            cache.Set(key, index, new MemoryCacheEntryOptions { SlidingExpiration = SlidingExpiration });
            return index;
        }
        finally
        {
            if (lazy.Value.IsCompleted)
                _building.TryRemove(key, out _);
        }
    }

    private DziTileIndex? Build(string zipPath)
    {
        using var zip = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
        var result = DziZipIndexer.Build(zip);
        if (!result.Success)
            logger.LogWarning("File {Path} is not a servable DZI zip: {Error}", zipPath, result.Error);
        return result.Index;
    }
}
