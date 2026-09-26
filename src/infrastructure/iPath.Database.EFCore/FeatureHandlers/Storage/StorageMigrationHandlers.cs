using iPath.Application.Contracts.Storage;
using iPath.Application.Features.Storage;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Storage;

public class GetStorageInstancesHandler(IStorageRegistry registry, IUserSession sess)
    : IRequestHandler<GetStorageInstancesQuery, Task<List<StorageInstanceDto>>>
{
    public Task<List<StorageInstanceDto>> Handle(GetStorageInstancesQuery request, CancellationToken ct)
    {
        sess.AssertInRole("Admin");
        return Task.FromResult(registry.All
            .Select(p => new StorageInstanceDto(p.InstanceName, p.Type.ToString(), p.Description, p == registry.Default))
            .OrderBy(p => p.Name)
            .ToList());
    }
}

public class ChangeCommunityStorageHandler(
    iPathDbContext db,
    IStorageRegistry registry,
    IStorageMigrationPlanner planner,
    IStorageMigrationQueue queue,
    IUserSession sess)
    : IRequestHandler<ChangeCommunityStorageCommand, Task<StorageMigrationDto?>>
{
    public async Task<StorageMigrationDto?> Handle(ChangeCommunityStorageCommand request, CancellationToken ct)
    {
        sess.AssertInRole("Admin");

        var community = await db.Communities.FirstOrDefaultAsync(c => c.Id == request.CommunityId, ct);
        Guard.Against.NotFound(request.CommunityId, community);

        var target = registry.All.FirstOrDefault(p => string.Equals(p.InstanceName, request.Instance, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Storage instance '{request.Instance}' is not configured.");

        if (await StorageMigrationGuard.IsActiveAsync(db, community.Id, ct))
            throw new InvalidOperationException("A storage migration for this community is still running.");

        // The community switches now; new uploads go to the target at once, existing files follow.
        var settings = community.Settings.Clone();
        settings.StorageInstance = target.InstanceName;
        community.Settings = settings;
        await db.SaveChangesAsync(ct);

        var migration = await planner.PlanCommunityAsync(community.Id,
            $"Community '{community.Name}' → {target.InstanceName}", ct);
        if (migration is not null)
            db.StorageMigrations.Add(migration);

        await db.SaveChangesAsync(ct);

        if (migration is null)
            return null;
        await queue.EnqueueAsync(migration.Id, ct);
        return migration.ToDto();
    }
}

public class GetStorageMigrationsHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetStorageMigrationsQuery, Task<List<StorageMigrationDto>>>
{
    public async Task<List<StorageMigrationDto>> Handle(GetStorageMigrationsQuery request, CancellationToken ct)
    {
        sess.AssertInRole("Admin");
        var migrations = await db.StorageMigrations.AsNoTracking()
            .Include(m => m.Items)
            .OrderByDescending(m => m.CreatedOn)
            .Take(100)
            .ToListAsync(ct);
        return migrations.Select(m => m.ToDto()).ToList();
    }
}

public class GetStorageMigrationItemsHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetStorageMigrationItemsQuery, Task<List<StorageMigrationItemDto>>>
{
    public async Task<List<StorageMigrationItemDto>> Handle(GetStorageMigrationItemsQuery request, CancellationToken ct)
    {
        sess.AssertInRole("Admin");
        var items = db.StorageMigrationItems.AsNoTracking().Where(i => i.MigrationId == request.MigrationId);
        if (request.FailedOnly)
            items = items.Where(i => i.Status == StorageMigrationItemStatus.Failed);
        return (await items.OrderBy(i => i.Id).ToListAsync(ct)).Select(i => i.ToDto()).ToList();
    }
}

public class RetryStorageMigrationHandler(iPathDbContext db, IStorageMigrationQueue queue, IUserSession sess)
    : IRequestHandler<RetryStorageMigrationCommand, Task<StorageMigrationDto>>
{
    public async Task<StorageMigrationDto> Handle(RetryStorageMigrationCommand request, CancellationToken ct)
    {
        sess.AssertInRole("Admin");
        var migration = await StorageMigrationGuard.LoadAsync(db, request.MigrationId, ct);
        if (migration.Status is StorageMigrationStatus.Purged)
            throw new InvalidOperationException("A purged migration cannot be resumed.");

        foreach (var item in migration.Items.Where(i => i.Status == StorageMigrationItemStatus.Failed))
        {
            item.Status = StorageMigrationItemStatus.Pending;
            item.Attempts = 0;
            item.ErrorMessage = null;
        }
        migration.Status = StorageMigrationStatus.Pending;
        migration.ErrorMessage = null;
        await db.SaveChangesAsync(ct);

        await queue.EnqueueAsync(migration.Id, ct);
        return migration.ToDto();
    }
}

public class CancelStorageMigrationHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<CancelStorageMigrationCommand, Task<StorageMigrationDto>>
{
    public async Task<StorageMigrationDto> Handle(CancelStorageMigrationCommand request, CancellationToken ct)
    {
        sess.AssertInRole("Admin");
        var migration = await StorageMigrationGuard.LoadAsync(db, request.MigrationId, ct);

        // Switched documents stay on the target, the rest stay on the source: all remain readable.
        if (migration.Status is StorageMigrationStatus.Pending or StorageMigrationStatus.Running)
        {
            migration.Status = StorageMigrationStatus.Cancelled;
            migration.CompletedOn = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return migration.ToDto();
    }
}

public class PurgeStorageMigrationHandler(
    iPathDbContext db,
    IStorageRegistry registry,
    IUserSession sess,
    ILogger<PurgeStorageMigrationHandler> logger)
    : IRequestHandler<PurgeStorageMigrationCommand, Task<StorageMigrationDto>>
{
    public async Task<StorageMigrationDto> Handle(PurgeStorageMigrationCommand request, CancellationToken ct)
    {
        sess.AssertInRole("Admin");
        var migration = await StorageMigrationGuard.LoadAsync(db, request.MigrationId, ct);
        if (migration.Status is not (StorageMigrationStatus.Completed or StorageMigrationStatus.CompletedWithErrors or StorageMigrationStatus.Cancelled))
            throw new InvalidOperationException($"Only a finished migration can be purged (status {migration.Status}).");

        foreach (var item in migration.Items.Where(i => i.Status == StorageMigrationItemStatus.Switched))
        {
            if (registry.Resolve(item.SourceInstance) is { } source)
            {
                await source.DeleteAsync(item.SourceKey, ct);
                await source.DeleteAsync(StorageKeys.TileIndexFor(item.SourceKey), ct);
            }

            var document = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == item.DocumentId, ct);
            if (document?.File is { } file)
            {
                var file2 = file.Clone();
                file2.RetiredLocations.RemoveAll(l => l.ProviderName == item.SourceInstance && l.StorageId == item.SourceKey);
                document.File = file2;
            }
        }

        if (!string.IsNullOrEmpty(migration.BackupPath) && Directory.Exists(migration.BackupPath))
        {
            try
            {
                Directory.Delete(migration.BackupPath, recursive: true);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Could not delete migration backup {Path}", migration.BackupPath);
            }
        }

        migration.Status = StorageMigrationStatus.Purged;
        migration.PurgedOn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return migration.ToDto();
    }
}

internal static class StorageMigrationGuard
{
    public static async Task<StorageMigration> LoadAsync(iPathDbContext db, Guid id, CancellationToken ct)
    {
        var migration = await db.StorageMigrations.Include(m => m.Items).FirstOrDefaultAsync(m => m.Id == id, ct);
        return Guard.Against.NotFound(id, migration);
    }

    public static Task<bool> IsActiveAsync(iPathDbContext db, Guid scopeId, CancellationToken ct) =>
        db.StorageMigrations.AnyAsync(m => m.ScopeId == scopeId
            && (m.Status == StorageMigrationStatus.Pending || m.Status == StorageMigrationStatus.Running), ct);
}
