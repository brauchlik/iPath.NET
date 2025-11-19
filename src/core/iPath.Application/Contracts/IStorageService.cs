namespace iPath.Application.Contracts;

public interface IStorageService
{
    string StoragePath { get; }

    Task<StorageRepsonse> PutNodeFileAsync(Guid NodeId, CancellationToken ctk = default!);
    Task<StorageRepsonse> PutNodeFileAsync(Node node, CancellationToken ctk = default!);

    Task<StorageRepsonse> GetNodeFileAsync(Guid NodeId, CancellationToken ctk = default!);
    Task<StorageRepsonse> GetNodeFileAsync(Node node, CancellationToken ctk = default!);


    Task<StorageRepsonse> PutNodeJsonAsync(Guid NodeId, CancellationToken ctk = default!);
    Task<StorageRepsonse> PutNodeJsonAsync(Node node, CancellationToken ctk = default!);
}


public record StorageRepsonse(bool Success, string? StorageId = null, string? Message = null!);