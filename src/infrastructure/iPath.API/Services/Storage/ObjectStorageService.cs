using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Ardalis.GuardClauses;
using iPath.Application.Contracts.Storage;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Storage;

/// <summary>
/// Document storage on the configured blob instances (LocalFiles, S3). New files go to the
/// registry's default instance; reads and deletes follow the instance recorded on each file.
/// Google Drive keeps its own <see cref="IRemoteStorageService"/> implementation.
/// </summary>
public class ObjectStorageService(
    IStorageRegistry registry,
    iPathDbContext db,
    IOptions<iPathConfig> opts,
    ILogger<ObjectStorageService> logger)
    : IRemoteStorageService
{
    public string ProviderName => registry.Default.InstanceName;
    public string RootStorageName => $"{registry.Default.InstanceName}: {registry.Default.Description}";
    public bool CanServeDirectly => registry.Default.Type == StorageInstanceType.LocalFiles;

    public async Task<bool> InitStorageAsync()
    {
        var ok = true;
        foreach (var provider in registry.All)
        {
            try
            {
                await provider.EnsureReadyAsync(CancellationToken.None);
                logger.LogInformation("Storage {Instance} ready: {Location}", provider.InstanceName, provider.Description);
            }
            catch (Exception ex)
            {
                ok = false;
                logger.LogError(ex, "Storage {Instance} ({Location}) is not available", provider.InstanceName, provider.Description);
            }
        }
        return ok;
    }

    public async Task<StorageRepsonse> PutFileAsync(Guid documentId, CancellationToken ct = default)
    {
        try
        {
            var document = await db.Documents
                .Include(n => n.ServiceRequest)
                .FirstOrDefaultAsync(n => n.Id == documentId, ct);
            Guard.Against.NotFound(documentId, document);
            if (document.ServiceRequest is null)
                return StorageRepsonse.Fail("Document does not belong to a case");

            var localFile = TempPath(documentId);
            if (!File.Exists(localFile))
                return StorageRepsonse.Fail($"Local file not found: {localFile}");

            var provider = registry.Default;
            var key = document.File.Storage is { } existing && registry.Resolve(existing.ProviderName) == provider
                ? StorageKeys.Resolve(existing, document.ServiceRequest.GroupId, document.ServiceRequestId)
                : StorageKeys.ForDocument(document.ServiceRequest.GroupId, document.ServiceRequestId, documentId);

            await provider.PutFileAsync(key, localFile, document.File.MimeType, ct);

            var tileIndex = localFile + ".tileindex";
            if (File.Exists(tileIndex))
                await provider.PutFileAsync(StorageKeys.TileIndexFor(key), tileIndex, "application/octet-stream", ct);

            document.File.Storage = new StorageInfo(provider.InstanceName, key);
            document.File.LastStorageExportDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Stored document {DocumentId} in {Instance} as {Key}", documentId, provider.InstanceName, key);
            return StorageRepsonse.Ok(document.File.Storage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error storing document {DocumentId}", documentId);
            return StorageRepsonse.Fail($"Error storing document {documentId}: {ex.Message}");
        }
    }

    public async Task<StorageRepsonse> GetFileAsync(Guid documentId, CancellationToken ct = default)
    {
        try
        {
            var (document, provider, key) = await LocateAsync(documentId, ignoreDeleted: false, ct);
            if (provider is null || key is null)
                return StorageRepsonse.Fail($"Document {documentId} is not in a configured storage instance");

            var target = TempPath(documentId);
            var partial = target + ".download";
            await using (var source = await provider.OpenReadAsync(key, ct))
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(output, ct);
            }
            File.Move(partial, target, overwrite: true);

            return StorageRepsonse.Ok(document!.File.Storage!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching document {DocumentId} from storage", documentId);
            return StorageRepsonse.Fail($"Error fetching document {documentId}: {ex.Message}");
        }
    }

    public async Task<StorageRepsonse> DeleteFileAsync(Guid documentId, CancellationToken ct = default)
    {
        try
        {
            var (document, provider, key) = await LocateAsync(documentId, ignoreDeleted: true, ct);
            if (document?.File?.Storage is null)
                return new StorageRepsonse(true);
            if (provider is null || key is null)
            {
                logger.LogWarning("Document {DocumentId} is stored in {Provider}, which is not a configured instance; nothing deleted",
                    documentId, document.File.Storage.ProviderName);
                return StorageRepsonse.Ok(document.File.Storage);
            }

            await provider.DeleteAsync(key, ct);
            await provider.DeleteAsync(StorageKeys.TileIndexFor(key), ct);
            logger.LogInformation("Deleted document {DocumentId} from {Instance} ({Key})", documentId, provider.InstanceName, key);
            return StorageRepsonse.Ok(document.File.Storage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting document {DocumentId} from storage", documentId);
            return StorageRepsonse.Fail($"Error deleting document {documentId}: {ex.Message}");
        }
    }

    public async Task<StorageRepsonse> PutServiceRequestJsonAsync(Guid id, CancellationToken ct = default)
    {
        // The case JSON makes a folder readable without iPath; it is kept for local storage only.
        if (registry.Default.Type != StorageInstanceType.LocalFiles)
            return new StorageRepsonse(true);

        try
        {
            var node = await db.ServiceRequests.AsNoTracking()
                .Include(n => n.Documents)
                .Include(n => n.Annotations)
                .FirstOrDefaultAsync(n => n.Id == id, ct);
            Guard.Against.NotFound(id, node);

            var jsonOpts = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                WriteIndented = true,
            };

            var temp = Path.Combine(opts.Value.TempDataPath, $"{id}.json");
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(node, jsonOpts), ct);
            try
            {
                await registry.Default.PutFileAsync($"{node.GroupId}/{node.Id}/{node.Id}.json", temp, "application/json", ct);
            }
            finally
            {
                File.Delete(temp);
            }
            return new StorageRepsonse(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing case JSON for {RequestId}", id);
            return StorageRepsonse.Fail($"Error writing case JSON for {id}: {ex.Message}");
        }
    }

    public Task<StorageRepsonse> DeleteServiceRequestJsonAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(new StorageRepsonse(true));

    // Keys are id-based, so renames never touch LocalFiles or S3.
    public Task RenameRequest(ServiceRequest request) => Task.CompletedTask;
    public Task RenameGroup(Group group) => Task.CompletedTask;
    public Task RenameCommunity(Community community) => Task.CompletedTask;

    // User upload folders are a Google Drive feature.
    public bool UserUploadFolderActive => false;
    public Task<UserUploadFolder> CreateUserUploadFolderAsync(Guid userId, CancellationToken ct) => throw DriveOnly();
    public Task DeleteUserUploadFolderAsync(Guid userId, CancellationToken ct) => throw DriveOnly();
    public Task<ServiceRequestUploadFolder> CreateRequestUploadFolderAsync(Guid serviceRequestId, Guid userId, CancellationToken ct) => throw DriveOnly();
    public Task DeleteRequestUploadFolderAsync(Guid folderId, CancellationToken ct) => throw DriveOnly();
    public Task<ScanExternalDocumentResponse> ScanUploadFolderAsync(ServiceRequestUploadFolder folder, CancellationToken ct = default) => throw DriveOnly();
    public Task<FolderImportResponse> ImportUploadFolderAsync(ServiceRequestUploadFolder folder, IReadOnlyList<string>? storageIds, CancellationToken ct = default) =>
        Task.FromResult(FolderImportResponse.Fail("Upload folders are only available with Google Drive storage"));

    private static NotSupportedException DriveOnly() => new("Upload folders are only available with Google Drive storage");

    private string TempPath(Guid documentId) => Path.Combine(opts.Value.TempDataPath, documentId.ToString());

    private async Task<(DocumentNode? Document, IStorageProvider? Provider, string? Key)> LocateAsync(Guid documentId, bool ignoreDeleted, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking().Include(n => n.ServiceRequest).AsQueryable();
        if (ignoreDeleted)
            query = query.IgnoreQueryFilters();
        var document = await query.FirstOrDefaultAsync(n => n.Id == documentId, ct);

        var storage = document?.File?.Storage;
        if (storage is null || document!.ServiceRequest is null)
            return (document, null, null);

        var provider = registry.Resolve(storage.ProviderName);
        return (document, provider, provider is null ? null : StorageKeys.Resolve(storage, document.ServiceRequest.GroupId, document.ServiceRequestId));
    }
}
