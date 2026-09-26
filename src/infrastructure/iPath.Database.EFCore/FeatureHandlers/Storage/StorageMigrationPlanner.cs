using iPath.Application.Contracts.Storage;

namespace iPath.EF.Core.FeatureHandlers.Storage;

public class StorageMigrationPlanner(iPathDbContext db, IStorageRegistry registry, IUserSession sess)
    : IStorageMigrationPlanner
{
    public async Task<StorageMigration?> PlanGroupAsync(Guid groupId, StorageMigrationKind kind, string description, CancellationToken ct)
    {
        var group = await db.Groups.AsNoTracking().Include(g => g.Community).FirstOrDefaultAsync(g => g.Id == groupId, ct);
        Guard.Against.NotFound(groupId, group);

        var target = registry.ForCommunity(group.Community?.Settings.StorageInstance);
        var documents = await Documents().Where(d => d.ServiceRequest.GroupId == groupId).ToListAsync(ct);
        return Plan(kind, groupId, description, target, documents);
    }

    public async Task<StorageMigration?> PlanCommunityAsync(Guid communityId, string description, CancellationToken ct)
    {
        var community = await db.Communities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == communityId, ct);
        Guard.Against.NotFound(communityId, community);

        var target = registry.ForCommunity(community.Settings.StorageInstance);
        var documents = await Documents().Where(d => d.ServiceRequest.Group.CommunityId == communityId).ToListAsync(ct);
        return Plan(StorageMigrationKind.CommunityStorageChange, communityId, description, target, documents);
    }

    // Case, group and community are loaded because Google Drive keys are built from their names.
    private IQueryable<DocumentNode> Documents() => db.Documents.AsNoTracking()
        .Include(d => d.ServiceRequest).ThenInclude(r => r.Group).ThenInclude(g => g.Community);

    private StorageMigration? Plan(StorageMigrationKind kind, Guid scopeId, string description, IStorageProvider target,
        IEnumerable<DocumentNode> documents)
    {
        var migration = new StorageMigration
        {
            Kind = kind,
            ScopeId = scopeId,
            Description = description,
            CreatedById = sess.User?.Id,
        };

        foreach (var document in documents)
        {
            var file = document.File;
            // Files not yet stored are uploaded to the new instance anyway; files on an instance
            // that is not a configured storage instance (the legacy Drive service) cannot be moved.
            if (file?.Storage is not { } stored || registry.Resolve(stored.ProviderName) is not { } source)
                continue;
            if (source.InstanceName == target.InstanceName)
                continue;

            migration.Items.Add(new StorageMigrationItem
            {
                MigrationId = migration.Id,
                DocumentId = document.Id,
                SourceInstance = source.InstanceName,
                SourceKey = StorageKeys.Resolve(stored, document.ServiceRequest.GroupId, document.ServiceRequestId),
                TargetInstance = target.InstanceName,
                TargetKey = StorageKeys.ForNewFile(target, document),
                Size = file.FileSize,
            });
        }

        return migration.Items.Count == 0 ? null : migration;
    }
}
