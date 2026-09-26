using System.Collections.Concurrent;
using System.Net.Http.Headers;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using iPath.Application.Contracts.Storage;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace iPath.Google.Storage;

/// <summary>
/// A Google Drive folder as a storage instance. Keys are folder paths below the instance's root
/// folder ("a/b/c/file"); folders are created on write. Keys, not Drive ids, are what iPath stores,
/// so the folder layout stays readable in Drive.
/// </summary>
public sealed class GoogleDriveStorageProvider : IStorageProvider, IDisposable
{
    private const string FolderMime = "application/vnd.google-apps.folder";

    private readonly DriveService _drive;
    private readonly string _rootFolderId;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, string> _folderIds = new();
    private readonly SemaphoreSlim _folderLock = new(1, 1);

    public GoogleDriveStorageProvider(string instanceName, StorageInstanceConfig cfg, ILogger logger)
    {
        InstanceName = instanceName;
        _rootFolderId = cfg.RootFolderId!;
        _logger = logger;

        GoogleCredential credential;
        using (var stream = File.OpenRead(cfg.ClientSecretPath!))
        {
            credential = GoogleCredential.FromStream(stream).CreateScoped(DriveService.Scope.Drive);
        }
        if (!string.IsNullOrEmpty(cfg.Username))
            credential = credential.CreateWithUser(cfg.Username); // domain-wide delegation

        _drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = string.IsNullOrEmpty(cfg.ApplicationName) ? "iPath.NET" : cfg.ApplicationName,
        });
        Description = $"Google Drive folder {_rootFolderId}";
    }

    public string InstanceName { get; }
    public StorageInstanceType Type => StorageInstanceType.GoogleDrive;
    public string Description { get; }

    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        var request = _drive.Files.Get(_rootFolderId);
        request.Fields = "id, name";
        request.SupportsAllDrives = true;
        await request.ExecuteAsync(ct);
    }

    public async Task<long?> GetLengthAsync(string key, CancellationToken ct) =>
        (await FindFileAsync(key, ct))?.Size;

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var file = await FindFileAsync(key, ct) ?? throw NotFound(key);
        var response = await _drive.HttpClient.GetAsync(MediaUrl(file.Id), HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct);
    }

    public BlobRange GetRange(string key, long offset, long length) =>
        new StreamRange(async ct =>
        {
            var file = await FindFileAsync(key, ct) ?? throw NotFound(key);
            using var request = new HttpRequestMessage(HttpMethod.Get, MediaUrl(file.Id));
            request.Headers.Range = new RangeHeaderValue(offset, offset + length - 1);
            var response = await _drive.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync(ct);
        }, offset, length);

    public async Task PutFileAsync(string key, string sourcePath, string? contentType, CancellationToken ct)
    {
        var (folderPath, name) = Split(key);
        var parentId = await GetFolderIdAsync(folderPath, create: true, ct);
        var existing = await FindChildAsync(parentId!, name, isFolder: false, ct);
        var mime = string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType;

        await using var content = File.OpenRead(sourcePath);
        global::Google.Apis.Upload.IUploadProgress progress;
        if (existing is null)
        {
            var create = _drive.Files.Create(new DriveFile { Name = name, Parents = [parentId!] }, content, mime);
            create.SupportsAllDrives = true;
            progress = await create.UploadAsync(ct);
        }
        else
        {
            var update = _drive.Files.Update(new DriveFile(), existing.Id, content, mime);
            update.SupportsAllDrives = true;
            progress = await update.UploadAsync(ct);
        }
        if (progress.Status != global::Google.Apis.Upload.UploadStatus.Completed)
            throw new IOException($"Upload of '{key}' to {InstanceName} failed: {progress.Exception?.Message}", progress.Exception);
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        var file = await FindFileAsync(key, ct);
        if (file is null)
            return;
        var request = _drive.Files.Delete(file.Id);
        request.SupportsAllDrives = true;
        await request.ExecuteAsync(ct);
    }

    public string? GetLocalPath(string key) => null;

    public void Dispose()
    {
        _drive.Dispose();
        _folderLock.Dispose();
    }

    private async Task<DriveFile?> FindFileAsync(string key, CancellationToken ct)
    {
        var (folderPath, name) = Split(key);
        var parentId = await GetFolderIdAsync(folderPath, create: false, ct);
        return parentId is null ? null : await FindChildAsync(parentId, name, isFolder: false, ct);
    }

    private async Task<string?> GetFolderIdAsync(string folderPath, bool create, CancellationToken ct)
    {
        if (folderPath.Length == 0)
            return _rootFolderId;
        if (_folderIds.TryGetValue(folderPath, out var cached))
            return cached;

        // Serialised so concurrent uploads into a new case do not create the same folder twice.
        await _folderLock.WaitAsync(ct);
        try
        {
            var parentId = _rootFolderId;
            var path = "";
            foreach (var segment in folderPath.Split('/'))
            {
                path = path.Length == 0 ? segment : $"{path}/{segment}";
                if (_folderIds.TryGetValue(path, out var known))
                {
                    parentId = known;
                    continue;
                }

                var folder = await FindChildAsync(parentId, segment, isFolder: true, ct);
                if (folder is null)
                {
                    if (!create)
                        return null;
                    var request = _drive.Files.Create(new DriveFile { Name = segment, MimeType = FolderMime, Parents = [parentId] });
                    request.Fields = "id";
                    request.SupportsAllDrives = true;
                    folder = await request.ExecuteAsync(ct);
                }
                _folderIds[path] = folder.Id;
                parentId = folder.Id;
            }
            return parentId;
        }
        finally
        {
            _folderLock.Release();
        }
    }

    private async Task<DriveFile?> FindChildAsync(string parentId, string name, bool isFolder, CancellationToken ct)
    {
        var request = _drive.Files.List();
        request.Q = $"'{parentId}' in parents and name = '{Escape(name)}' and trashed = false and mimeType {(isFolder ? "=" : "!=")} '{FolderMime}'";
        request.Fields = "files(id, name, size)";
        request.SupportsAllDrives = true;
        request.IncludeItemsFromAllDrives = true;
        request.PageSize = 2;
        var result = await request.ExecuteAsync(ct);
        if (result.Files.Count > 1)
            _logger.LogWarning("Drive folder {Parent} holds several items named {Name}; using the first", parentId, name);
        return result.Files.FirstOrDefault();
    }

    private static (string FolderPath, string Name) Split(string key)
    {
        var slash = key.LastIndexOf('/');
        return slash < 0 ? ("", key) : (key[..slash], key[(slash + 1)..]);
    }

    // Drive query strings quote with ' and escape with \.
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

    private static string MediaUrl(string fileId) =>
        $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media&supportsAllDrives=true";

    private FileNotFoundException NotFound(string key) => new($"No object '{key}' in storage '{InstanceName}'.", key);
}
