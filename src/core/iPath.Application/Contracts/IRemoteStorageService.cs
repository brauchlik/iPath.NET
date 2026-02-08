namespace iPath.Application.Contracts;

public interface IRemoteStorageService
{
    string ProviderName { get; }

    Task<StorageRepsonse> PutFileAsync(Guid Id, CancellationToken ctk = default!);
    // Task<StorageRepsonse> PutFileAsync(DocumentNode document, CancellationToken ctk = default!);

    Task<StorageRepsonse> GetFileAsync(Guid Id, CancellationToken ctk = default!);
    // Task<StorageRepsonse> GetFileAsync(DocumentNode document, CancellationToken ctk = default!);
    Task<StorageRepsonse> DeleteFileAsync(Guid Id, CancellationToken ctk = default!);

    Task<StorageRepsonse> PutServiceRequestJsonAsync(Guid Id, CancellationToken ctk = default!);
    //Task<StorageRepsonse> PutServiceRequestJsonAsync(ServiceRequest request, CancellationToken ctk = default!);
    Task<StorageRepsonse> DeleteServiceRequestJsonAsync(Guid Id, CancellationToken ctk = default!);


    Task<string?> CreateViewLink(DocumentNode doc, CancellationToken ct = default);

    Task RenameRequest(ServiceRequest request);
    Task RenameGroup(Group group);
    Task RenameCommunity(Community community);


    bool UserUploadFolderActive { get; }
    Task<UserUploadFolder> CreateUserUploadFolderAsync(Guid userId, CancellationToken ct);
    Task DeleteUserUploadFolderAsync(Guid userId, CancellationToken ct);

    Task<ServiceRequestUploadFolder> CreateRequestUploadFolderAsync(Guid ServiceRequestId, Guid UserId, CancellationToken ct);
    Task DeleteRequestUploadFolderAsync(Guid FolderId, CancellationToken ct);

    Task<ScanExternalDocumentResponse> ScanUploadFolderAsync(ServiceRequestUploadFolder folder, CancellationToken ctk = default!);
    Task<FolderImportResponse> ImportUploadFolderAsync(ServiceRequestUploadFolder folder, IReadOnlyList<string>? storageIds, CancellationToken ctk = default!);
}


public record StorageRepsonse(bool Success, StorageInfo? Storage = null, string? Message = null!)
{
    public static StorageRepsonse Ok(StorageInfo storage) => new StorageRepsonse(true, Storage: storage);
    public static StorageRepsonse Fail(string error) => new StorageRepsonse(false, Message: error);
}

public record FolderImportResponse(bool Success, int ImportCount, string? Message = null)
{
    public static FolderImportResponse Ok(int count) => new FolderImportResponse(true, count);
    public static FolderImportResponse Fail(string msg) => new FolderImportResponse(false, 0, msg);
}