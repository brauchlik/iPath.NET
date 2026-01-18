namespace iPath.Application.Contracts;

public interface IStorageService
{
    string StoragePath { get; }

    Task<StorageRepsonse> PutFileAsync(Guid DocumentId, CancellationToken ctk = default!);
    Task<StorageRepsonse> PutFileAsync(DocumentNode document, CancellationToken ctk = default!);

    Task<StorageRepsonse> GetFileAsync(Guid DocumentId, CancellationToken ctk = default!);
    Task<StorageRepsonse> GetFileAsync(DocumentNode document, CancellationToken ctk = default!);

    Task<StorageRepsonse> PutServiceRequestJsonAsync(Guid Id, CancellationToken ctk = default!);
    Task<StorageRepsonse> PutServiceRequestJsonAsync(ServiceRequest request, CancellationToken ctk = default!);
}


public record StorageRepsonse(bool Success, string? StorageId = null, string? Message = null!);