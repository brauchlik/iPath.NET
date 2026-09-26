namespace iPath.Application.Features.Storage;

public record StorageInstanceDto(string Name, string Type, string Description, bool IsDefault);

public record GetStorageInstancesQuery()
    : IRequest<GetStorageInstancesQuery, Task<List<StorageInstanceDto>>>;

/// <summary>
/// Switches a community to another storage instance. Its files are moved in the background by a
/// storage migration; they stay readable from the old instance until each one is moved.
/// </summary>
public record ChangeCommunityStorageCommand(Guid CommunityId, string Instance)
    : IRequest<ChangeCommunityStorageCommand, Task<StorageMigrationDto?>>;

public record GetStorageMigrationsQuery()
    : IRequest<GetStorageMigrationsQuery, Task<List<StorageMigrationDto>>>;

public record GetStorageMigrationItemsQuery(Guid MigrationId, bool FailedOnly = false)
    : IRequest<GetStorageMigrationItemsQuery, Task<List<StorageMigrationItemDto>>>;

public record RetryStorageMigrationCommand(Guid MigrationId)
    : IRequest<RetryStorageMigrationCommand, Task<StorageMigrationDto>>;

public record CancelStorageMigrationCommand(Guid MigrationId)
    : IRequest<CancelStorageMigrationCommand, Task<StorageMigrationDto>>;

/// <summary>Deletes the retired source copies and the local backup of a finished migration.</summary>
public record PurgeStorageMigrationCommand(Guid MigrationId)
    : IRequest<PurgeStorageMigrationCommand, Task<StorageMigrationDto>>;

public record StorageMigrationDto(
    Guid Id,
    string Kind,
    string Status,
    Guid ScopeId,
    string? Description,
    DateTime CreatedOn,
    DateTime? StartedOn,
    DateTime? CompletedOn,
    DateTime? PurgedOn,
    int Total,
    int Switched,
    int Failed,
    int Skipped,
    long Bytes,
    string? BackupPath,
    string? ErrorMessage);

public record StorageMigrationItemDto(
    Guid Id,
    Guid DocumentId,
    string Status,
    string SourceInstance,
    string SourceKey,
    string TargetInstance,
    string TargetKey,
    long? Size,
    string? Sha256,
    int Attempts,
    string? ErrorMessage,
    DateTime? CompletedOn);

public static class StorageMigrationMapping
{
    public static StorageMigrationDto ToDto(this StorageMigration m) => new(
        m.Id, m.Kind.ToString(), m.Status.ToString(), m.ScopeId, m.Description,
        m.CreatedOn, m.StartedOn, m.CompletedOn, m.PurgedOn,
        m.Items.Count,
        m.Items.Count(i => i.Status == StorageMigrationItemStatus.Switched),
        m.Items.Count(i => i.Status == StorageMigrationItemStatus.Failed),
        m.Items.Count(i => i.Status == StorageMigrationItemStatus.Skipped),
        m.Items.Where(i => i.Status == StorageMigrationItemStatus.Switched).Sum(i => i.Size ?? 0),
        m.BackupPath, m.ErrorMessage);

    public static StorageMigrationItemDto ToDto(this StorageMigrationItem i) => new(
        i.Id, i.DocumentId, i.Status.ToString(), i.SourceInstance, i.SourceKey, i.TargetInstance, i.TargetKey,
        i.Size, i.Sha256, i.Attempts, i.ErrorMessage, i.CompletedOn);
}
