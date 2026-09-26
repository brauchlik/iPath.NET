using iPath.Application.Contracts.Storage;

namespace iPath.API.Services.Storage.Providers;

public sealed class LocalFileStorageProvider(string instanceName, string rootPath) : IStorageProvider
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public string InstanceName => instanceName;
    public StorageInstanceType Type => StorageInstanceType.LocalFiles;
    public string Description => _root;

    public Task EnsureReadyAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        return Task.CompletedTask;
    }

    public Task<long?> GetLengthAsync(string key, CancellationToken ct)
    {
        var file = new FileInfo(PathFor(key));
        return Task.FromResult<long?>(file.Exists ? file.Length : null);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
            throw new FileNotFoundException($"No object '{key}' in storage '{InstanceName}'.", path);
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    public BlobRange GetRange(string key, long offset, long length) => new PhysicalFileRange(PathFor(key), offset, length);

    public async Task PutFileAsync(string key, string sourcePath, string? contentType, CancellationToken ct)
    {
        var target = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        // Copy under a temporary name first, so a reader never sees a half-written file.
        var partial = target + ".partial";
        await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        {
            await source.CopyToAsync(output, ct);
        }
        File.Move(partial, target, overwrite: true);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public string? GetLocalPath(string key)
    {
        var path = PathFor(key);
        return File.Exists(path) ? path : null;
    }

    private string PathFor(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Storage key '{key}' escapes the storage root.", nameof(key));
        return path;
    }
}
