using System.Collections.Concurrent;
using iPath.Application.Contracts.Storage;
using iPath.Application.Features.Conversion.Dzi;
using Microsoft.Extensions.Caching.Memory;

namespace iPath.API.Services.Wsi;

/// <summary>
/// Keeps the tile index of recently viewed DZI zips in memory, so a tile request is an index
/// lookup plus one range read. The index comes from the sidecar stored next to the zip
/// (<c>{zip}.tileindex</c>); a local zip without one is indexed directly.
/// </summary>
public sealed class DziTileIndexCache(IMemoryCache cache, ILogger<DziTileIndexCache> logger)
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(20);

    // A viewer opening a slide fires many tile requests at once; only one of them builds the index.
    private readonly ConcurrentDictionary<string, Lazy<Task<DziTileIndex?>>> _building = new();

    /// <returns>The index of a local zip, or null when the file is not an indexable DZI zip.</returns>
    public Task<DziTileIndex?> GetAsync(string zipPath, CancellationToken ct)
    {
        var file = new FileInfo(zipPath);
        if (!file.Exists)
            return Task.FromResult<DziTileIndex?>(null);

        // Length and write time in the key: a replaced file (e.g. repacked) gets a fresh index.
        var key = $"dzi-index:{file.FullName}:{file.Length}:{file.LastWriteTimeUtc.Ticks}";
        return GetOrBuildAsync(key, () => Task.Run(() => LoadLocal(file)), ct);
    }

    /// <returns>
    /// The index stored next to a zip on a (remote) instance, or null when there is none — the
    /// caller then falls back to a local copy of the zip.
    /// </returns>
    public Task<DziTileIndex?> GetAsync(IStorageProvider provider, string zipKey, CancellationToken ct) =>
        GetOrBuildAsync($"dzi-index:{provider.InstanceName}:{zipKey}", () => LoadRemoteAsync(provider, zipKey), ct);

    private async Task<DziTileIndex?> GetOrBuildAsync(string key, Func<Task<DziTileIndex?>> load, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out DziTileIndex? cached))
            return cached;

        var lazy = _building.GetOrAdd(key, _ => new Lazy<Task<DziTileIndex?>>(load));
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

    private DziTileIndex? LoadLocal(FileInfo zip)
    {
        var sidecar = new FileInfo(zip.FullName + ".tileindex");
        if (sidecar.Exists)
        {
            try
            {
                using var stream = sidecar.OpenRead();
                var stored = DziTileIndex.ReadFrom(stream);
                if (stored.ZipLength == zip.Length)
                    return stored;
            }
            catch (InvalidDataException ex)
            {
                logger.LogWarning(ex, "Ignoring unreadable tile index {Path}", sidecar.FullName);
            }
        }

        using var file = new FileStream(zip.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
        var result = DziZipIndexer.Build(file);
        if (!result.Success)
            logger.LogWarning("File {Path} is not a servable DZI zip: {Error}", zip.FullName, result.Error);
        return result.Index;
    }

    private async Task<DziTileIndex?> LoadRemoteAsync(IStorageProvider provider, string zipKey)
    {
        try
        {
            await using var stream = await provider.OpenReadAsync(StorageKeys.TileIndexFor(zipKey), CancellationToken.None);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            return DziTileIndex.ReadFrom(buffer);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "Ignoring unreadable tile index for {Key} in {Instance}", zipKey, provider.InstanceName);
            return null;
        }
    }
}
