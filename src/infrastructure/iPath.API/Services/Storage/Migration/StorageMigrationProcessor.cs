using System.Security.Cryptography;
using iPath.Application.Contracts.Storage;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Storage.Migration;

/// <summary>
/// Runs one storage migration: per document, copy source → local backup → target, verify the
/// target by SHA-256, then switch the document to the target and retire the source — in one save,
/// so a document is always readable from exactly one recorded location. Safe to re-run: switched
/// documents are skipped, anything else starts over.
/// </summary>
public sealed class StorageMigrationProcessor(
    iPathDbContext db,
    IStorageRegistry registry,
    IMemoryCache cache,
    IOptions<StorageConfig> storageOpts,
    IOptions<iPathConfig> ipathOpts,
    ILogger<StorageMigrationProcessor> logger)
{
    private const int MaxAttempts = 3;

    public async Task RunAsync(Guid migrationId, CancellationToken ct)
    {
        var migration = await db.StorageMigrations.Include(m => m.Items).FirstOrDefaultAsync(m => m.Id == migrationId, ct);
        if (migration is null || migration.Status is not (StorageMigrationStatus.Pending or StorageMigrationStatus.Running))
            return;

        migration.Status = StorageMigrationStatus.Running;
        migration.StartedOn ??= DateTime.UtcNow;
        migration.BackupPath ??= Path.Combine(BackupRoot(), migration.Id.ToString());
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Storage migration {MigrationId} started: {Description}", migration.Id, migration.Description);

        foreach (var item in migration.Items.Where(i => i.Status is StorageMigrationItemStatus.Pending or StorageMigrationItemStatus.Copied).OrderBy(i => i.Id))
        {
            await db.Entry(migration).ReloadAsync(ct);
            if (migration.Status == StorageMigrationStatus.Cancelled)
            {
                logger.LogInformation("Storage migration {MigrationId} cancelled", migration.Id);
                return;
            }
            await ProcessItemAsync(migration, item, ct);
        }

        migration.Status = migration.Items.Any(i => i.Status == StorageMigrationItemStatus.Failed)
            ? StorageMigrationStatus.CompletedWithErrors
            : StorageMigrationStatus.Completed;
        migration.CompletedOn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Storage migration {MigrationId} finished: {Status}", migration.Id, migration.Status);
    }

    private async Task ProcessItemAsync(StorageMigration migration, StorageMigrationItem item, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == item.DocumentId, ct);
        var current = document?.File?.Storage;
        if (current is null)
        {
            await FinishAsync(item, StorageMigrationItemStatus.Skipped, "Document deleted or not stored", ct);
            return;
        }
        if (registry.Resolve(current.ProviderName)?.InstanceName == item.TargetInstance && current.StorageId == item.TargetKey)
        {
            await FinishAsync(item, StorageMigrationItemStatus.Switched, null, ct);
            return;
        }
        if (registry.Resolve(current.ProviderName)?.InstanceName != item.SourceInstance)
        {
            await FinishAsync(item, StorageMigrationItemStatus.Skipped, $"Document moved to {current.ProviderName} meanwhile", ct);
            return;
        }

        var source = registry.Resolve(item.SourceInstance);
        var target = registry.Resolve(item.TargetInstance);
        if (source is null || target is null)
        {
            await FinishAsync(item, StorageMigrationItemStatus.Failed, "Source or target storage instance is not configured", ct);
            return;
        }

        while (true)
        {
            try
            {
                item.Attempts++;
                var backup = Path.Combine(migration.BackupPath!, item.TargetKey.Replace('/', Path.DirectorySeparatorChar));
                var (size, sha256) = await CopyVerifiedAsync(source, item.SourceKey, target, item.TargetKey, backup, document!.File.MimeType, ct);

                var sourceIndex = StorageKeys.TileIndexFor(item.SourceKey);
                if (await source.GetLengthAsync(sourceIndex, ct) is not null)
                    await CopyVerifiedAsync(source, sourceIndex, target, StorageKeys.TileIndexFor(item.TargetKey), backup + ".tileindex", "application/octet-stream", ct);

                item.Size = size;
                item.Sha256 = sha256;
                item.Status = StorageMigrationItemStatus.Copied;

                // Switch and retire in the same save as the item's status.
                var file = document.File.Clone();
                file.RetiredLocations.Add(current);
                file.Storage = new StorageInfo(target.InstanceName, item.TargetKey);
                document.File = file;
                await FinishAsync(item, StorageMigrationItemStatus.Switched, null, ct);

                cache.Remove($"document-file-metadata:{item.DocumentId}");
                logger.LogInformation("Moved document {DocumentId} {Source}:{SourceKey} → {Target}:{TargetKey} ({Size} bytes)",
                    item.DocumentId, source.InstanceName, item.SourceKey, target.InstanceName, item.TargetKey, size);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Moving document {DocumentId} failed (attempt {Attempt}/{Max})", item.DocumentId, item.Attempts, MaxAttempts);
                if (item.Attempts >= MaxAttempts)
                {
                    await FinishAsync(item, StorageMigrationItemStatus.Failed, ex.Message, ct);
                    return;
                }
                item.ErrorMessage = ex.Message;
                await db.SaveChangesAsync(ct);
                await Task.Delay(TimeSpan.FromSeconds(2 << item.Attempts), ct);
            }
        }
    }

    /// <summary>Source → backup file (hashed) → target, then re-reads the target and compares the hash.</summary>
    private static async Task<(long Size, string Sha256)> CopyVerifiedAsync(
        IStorageProvider source, string sourceKey, IStorageProvider target, string targetKey, string backupFile, string? contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(backupFile)!);
        string sourceHash;
        await using (var input = await source.OpenReadAsync(sourceKey, ct))
        await using (var output = new FileStream(backupFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        {
            sourceHash = await CopyHashingAsync(input, output, ct);
        }

        await target.PutFileAsync(targetKey, backupFile, contentType, ct);

        string targetHash;
        await using (var copy = await target.OpenReadAsync(targetKey, ct))
        {
            targetHash = await CopyHashingAsync(copy, Stream.Null, ct);
        }
        if (targetHash != sourceHash)
            throw new InvalidDataException($"Verification failed for {targetKey}: SHA-256 {targetHash} != {sourceHash}");

        return (new FileInfo(backupFile).Length, sourceHash);
    }

    private static async Task<string> CopyHashingAsync(Stream input, Stream output, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private async Task FinishAsync(StorageMigrationItem item, StorageMigrationItemStatus status, string? error, CancellationToken ct)
    {
        item.Status = status;
        item.ErrorMessage = error;
        item.CompletedOn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private string BackupRoot()
    {
        if (!string.IsNullOrEmpty(storageOpts.Value.MigrationBackupPath))
            return storageOpts.Value.MigrationBackupPath;
        var root = !string.IsNullOrEmpty(ipathOpts.Value.DataRoot)
            ? ipathOpts.Value.DataRoot
            : Path.GetDirectoryName(Path.GetFullPath(ipathOpts.Value.TempDataPath))!;
        return Path.Combine(root, "storage-backup");
    }
}
