namespace iPath.Application.Contracts;

public interface IRemoteStorageService
{
    // Task<StorageRepsonse> PutFileAsync(Guid DocumentId, CancellationToken ctk = default!);
    Task<StorageRepsonse> PutFileAsync(DocumentNode document, CancellationToken ctk = default!);

    // Task<StorageRepsonse> GetFileAsync(Guid DocumentId, CancellationToken ctk = default!);
    Task<StorageRepsonse> GetFileAsync(DocumentNode document, CancellationToken ctk = default!);

    // Task<StorageRepsonse> PutServiceRequestJsonAsync(Guid Id, CancellationToken ctk = default!);
    Task<StorageRepsonse> PutServiceRequestJsonAsync(ServiceRequest request, CancellationToken ctk = default!);


    Task<string?> CreateViewLink(DocumentNode doc, CancellationToken ct = default);
    Task<int> ScanNewFilesAsync(Guid requestId, CancellationToken ctk = default!);


    Task RenameRequest(ServiceRequest request);
    Task RenameGroup(Group group);
    Task RenameCommunity(Community community);

}


public record StorageRepsonse(bool Success, string? StorageId = null, string? Message = null!)
{
    public static StorageRepsonse Ok(string storageId) => new StorageRepsonse(true, StorageId: storageId);
    public static StorageRepsonse Fail(string error) => new StorageRepsonse(false, Message: error);
}