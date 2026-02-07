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


    bool UserUploadFolderActive { get; }
    Task CreateUserUploadFolderAsync(User user, CancellationToken ct);
    Task DeleteUserUploadFolderAsync(User user, CancellationToken ct);

    Task<ServiceRequestUploadFolder> CreateRequestUploadFolderAsync(Guid ServiceRequestId, Guid UserId, CancellationToken ct);
    Task DeleteRequestUploadFolderAsync(Guid FolderId, CancellationToken ct);

    Task<ScanExternalDocumentResponse> ScanNewFilesAsync(ServiceRequestUploadFolder folder, CancellationToken ctk = default!);
    Task ImportNewFilesAsync(ServiceRequestUploadFolder folder, IReadOnlyList<string> storageIds, CancellationToken ctk = default!);


    Task RenameRequest(ServiceRequest request);
    Task RenameGroup(Group group);
    Task RenameCommunity(Community community);

}


public record StorageRepsonse(bool Success, string? StorageId = null, string? Message = null!)
{
    public static StorageRepsonse Ok(string storageId) => new StorageRepsonse(true, StorageId: storageId);
    public static StorageRepsonse Fail(string error) => new StorageRepsonse(false, Message: error);
}