using iPath.Domain.Config;

namespace iPath.Application.Contracts.Storage;

/// <summary>
/// One configured storage instance: keys and bytes only. Knows nothing about documents, cases or
/// groups — <see cref="StorageKeys"/> and the document storage service map those to keys.
/// </summary>
public interface IStorageProvider
{
    string InstanceName { get; }
    StorageInstanceType Type { get; }

    /// <summary>Human-readable location, for admin pages and logs.</summary>
    string Description { get; }

    /// <summary>Creates the root folder or bucket if it is missing.</summary>
    Task EnsureReadyAsync(CancellationToken ct);

    /// <returns>The object's size in bytes, or null when there is no object under <paramref name="key"/>.</returns>
    Task<long?> GetLengthAsync(string key, CancellationToken ct);

    /// <exception cref="FileNotFoundException">No object under <paramref name="key"/>.</exception>
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);

    /// <summary>
    /// Describes a byte range of an object without reading it, so the caller can pick the
    /// cheapest transport (zero-copy send-file for local files, a streamed range for S3).
    /// </summary>
    BlobRange GetRange(string key, long offset, long length);

    Task PutFileAsync(string key, string sourcePath, string? contentType, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);

    /// <summary>The object's path on this machine, or null for remote instances.</summary>
    string? GetLocalPath(string key);
}

public abstract record BlobRange(long Offset, long Length);

public sealed record PhysicalFileRange(string Path, long Offset, long Length) : BlobRange(Offset, Length);

public sealed record StreamRange(Func<CancellationToken, Task<Stream>> Open, long Offset, long Length) : BlobRange(Offset, Length);

public interface IStorageRegistry
{
    /// <summary>The instance new files are written to.</summary>
    IStorageProvider Default { get; }

    IReadOnlyCollection<IStorageProvider> All { get; }

    /// <summary>
    /// The instance a stored file names in its <c>StorageInfo.ProviderName</c>, or null when it is
    /// not one of the configured instances (e.g. GoogleDrive, handled by its own service).
    /// </summary>
    IStorageProvider? Resolve(string? providerName);
}
