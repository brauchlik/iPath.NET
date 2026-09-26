namespace iPath.Domain.Entities;

// Only a change of storage instance migrates files. Moves within the same instance (a case to
// another group of its community, a group to a community on the same instance) keep the stored
// keys and change nothing in storage.
public enum StorageMigrationKind
{
    /// <summary>A group moved to a main community on another storage instance.</summary>
    GroupMove,
    /// <summary>A community switched to another storage instance.</summary>
    CommunityStorageChange,
}

public enum StorageMigrationStatus
{
    Pending,
    Running,
    Completed,
    /// <summary>Finished, but at least one document could not be moved.</summary>
    CompletedWithErrors,
    Cancelled,
    /// <summary>Retired source copies and the backup have been deleted.</summary>
    Purged,
}

public enum StorageMigrationItemStatus
{
    Pending,
    /// <summary>The target copy exists and is verified.</summary>
    Copied,
    /// <summary>The document now reads from the target; the source is retired.</summary>
    Switched,
    Failed,
    /// <summary>Nothing to move (document deleted or changed meanwhile).</summary>
    Skipped,
}

/// <summary>
/// Moves the stored files of a group or community to the storage instance its (new) community
/// uses. The relation itself is switched when the job is created; each
/// document keeps being read from its old location until its item switches.
/// </summary>
public class StorageMigration
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public StorageMigrationKind Kind { get; set; }
    public StorageMigrationStatus Status { get; set; } = StorageMigrationStatus.Pending;

    /// <summary>The group or community being migrated (depending on <see cref="Kind"/>).</summary>
    public Guid ScopeId { get; set; }
    public string? Description { get; set; }

    public Guid? CreatedById { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? StartedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public DateTime? PurgedOn { get; set; }

    /// <summary>Local folder with a copy of every migrated document, kept until the purge.</summary>
    public string? BackupPath { get; set; }
    public string? ErrorMessage { get; set; }

    public ICollection<StorageMigrationItem> Items { get; set; } = [];
}

public class StorageMigrationItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid MigrationId { get; set; }
    public StorageMigration Migration { get; set; } = null!;

    public Guid DocumentId { get; set; }
    public StorageMigrationItemStatus Status { get; set; } = StorageMigrationItemStatus.Pending;

    public string SourceInstance { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string TargetInstance { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;

    public long? Size { get; set; }
    public string? Sha256 { get; set; }
    public int Attempts { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedOn { get; set; }
}
