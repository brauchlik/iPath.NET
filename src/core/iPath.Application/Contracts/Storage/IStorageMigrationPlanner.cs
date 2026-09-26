namespace iPath.Application.Contracts.Storage;

/// <summary>
/// Creates storage migrations for files whose group or community now belongs to another storage
/// instance. The relation (group → community, community → instance) is already switched by the
/// caller; the migration moves the files behind it.
/// </summary>
public interface IStorageMigrationPlanner
{
    /// <returns>The created migration (not yet saved), or null when no file has to move.</returns>
    Task<StorageMigration?> PlanGroupAsync(Guid groupId, StorageMigrationKind kind, string description, CancellationToken ct);

    /// <returns>The created migration (not yet saved), or null when no file has to move.</returns>
    Task<StorageMigration?> PlanCommunityAsync(Guid communityId, string description, CancellationToken ct);
}

public interface IStorageMigrationQueue
{
    ValueTask EnqueueAsync(Guid migrationId, CancellationToken ct = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
}

public static class StorageRegistryExtensions
{
    /// <summary>The instance a community's files belong to: its own, else the default.</summary>
    public static IStorageProvider ForCommunity(this IStorageRegistry registry, string? communityInstance) =>
        registry.Resolve(communityInstance) ?? registry.Default;
}
