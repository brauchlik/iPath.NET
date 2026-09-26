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
        var documents = await db.Documents.AsNoTracking()
            .Where(d => d.ServiceRequest.GroupId == groupId)
            .Select(d => new { d.Id, d.ServiceRequestId, d.File })
            .ToListAsync(ct);

        return Plan(kind, groupId, description, target, documents.Select(d => (d.Id, groupId, d.ServiceRequestId, d.File)));
    }

    public async Task<StorageMigration?> PlanCommunityAsync(Guid communityId, string description, CancellationToken ct)
    {
        var community = await db.Communities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == communityId, ct);
        Guard.Against.NotFound(communityId, community);

        var target = registry.ForCommunity(community.Settings.StorageInstance);
        var documents = await db.Documents.AsNoTracking()
            .Where(d => d.ServiceRequest.Group.CommunityId == communityId)
            .Select(d => new { d.Id, d.ServiceRequest.GroupId, d.ServiceRequestId, d.File })
            .ToListAsync(ct);

        return Plan(StorageMigrationKind.CommunityStorageChange, communityId, description, target,
            documents.Select(d => (d.Id, d.GroupId, d.ServiceRequestId, d.File)));
    }

    private StorageMigration? Plan(StorageMigrationKind kind, Guid scopeId, string description, IStorageProvider target,
        IEnumerable<(Guid DocumentId, Guid GroupId, Guid RequestId, NodeFile? File)> documents)
    {
        var migration = new StorageMigration
        {
            Kind = kind,
            ScopeId = scopeId,
            Description = description,
            CreatedById = sess.User?.Id,
        };

        foreach (var (documentId, groupId, requestId, file) in documents)
        {
            // Files not yet stored are uploaded to the new instance anyway; files on an instance
            // that is not configured here (e.g. Google Drive) cannot be moved by a migration.
            if (file?.Storage is not { } stored || registry.Resolve(stored.ProviderName) is not { } source)
                continue;
            if (source.InstanceName == target.InstanceName)
                continue;

            migration.Items.Add(new StorageMigrationItem
            {
                MigrationId = migration.Id,
                DocumentId = documentId,
                SourceInstance = source.InstanceName,
                SourceKey = StorageKeys.Resolve(stored, groupId, requestId),
                TargetInstance = target.InstanceName,
                TargetKey = StorageKeys.ForDocument(groupId, requestId, documentId),
                Size = file.FileSize,
            });
        }

        return migration.Items.Count == 0 ? null : migration;
    }
}
