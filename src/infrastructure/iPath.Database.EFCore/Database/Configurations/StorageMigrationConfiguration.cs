using iPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace iPath_EFCore.Database.Configurations;

internal class StorageMigrationConfiguration : IEntityTypeConfiguration<StorageMigration>
{
    public void Configure(EntityTypeBuilder<StorageMigration> b)
    {
        b.ToTable("storage_migrations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.Kind).IsRequired().HasColumnName("kind").HasConversion<int>();
        b.Property(x => x.Status).IsRequired().HasColumnName("status").HasConversion<int>();
        b.Property(x => x.ScopeId).IsRequired().HasColumnName("scope_id");
        b.Property(x => x.Description).HasMaxLength(500).HasColumnName("description");
        b.Property(x => x.CreatedById).HasColumnName("created_by");
        b.Property(x => x.CreatedOn).IsRequired().HasColumnName("created_on");
        b.Property(x => x.StartedOn).HasColumnName("started_on");
        b.Property(x => x.CompletedOn).HasColumnName("completed_on");
        b.Property(x => x.PurgedOn).HasColumnName("purged_on");
        b.Property(x => x.BackupPath).HasMaxLength(1000).HasColumnName("backup_path");
        b.Property(x => x.ErrorMessage).HasMaxLength(2000).HasColumnName("error_message");

        b.HasIndex(x => x.Status);

        b.HasMany(x => x.Items)
            .WithOne(i => i.Migration)
            .HasForeignKey(i => i.MigrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal class StorageMigrationItemConfiguration : IEntityTypeConfiguration<StorageMigrationItem>
{
    public void Configure(EntityTypeBuilder<StorageMigrationItem> b)
    {
        b.ToTable("storage_migration_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.MigrationId).IsRequired().HasColumnName("migration_id");
        b.Property(x => x.DocumentId).IsRequired().HasColumnName("document_id");
        b.Property(x => x.Status).IsRequired().HasColumnName("status").HasConversion<int>();
        b.Property(x => x.SourceInstance).IsRequired().HasMaxLength(100).HasColumnName("source_instance");
        b.Property(x => x.SourceKey).IsRequired().HasMaxLength(500).HasColumnName("source_key");
        b.Property(x => x.TargetInstance).IsRequired().HasMaxLength(100).HasColumnName("target_instance");
        b.Property(x => x.TargetKey).IsRequired().HasMaxLength(500).HasColumnName("target_key");
        b.Property(x => x.Size).HasColumnName("size");
        b.Property(x => x.Sha256).HasMaxLength(64).HasColumnName("sha256");
        b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.ErrorMessage).HasMaxLength(2000).HasColumnName("error_message");
        b.Property(x => x.CompletedOn).HasColumnName("completed_on");

        b.HasIndex(x => new { x.MigrationId, x.DocumentId }).IsUnique();
        b.HasIndex(x => x.DocumentId);
    }
}
