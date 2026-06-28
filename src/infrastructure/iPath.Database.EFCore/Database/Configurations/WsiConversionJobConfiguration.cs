using iPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace iPath_EFCore.Database.Configurations;

internal class WsiConversionJobConfiguration : IEntityTypeConfiguration<WsiConversionJob>
{
    public void Configure(EntityTypeBuilder<WsiConversionJob> b)
    {
        b.ToTable("wsi_conversion_jobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.DocumentId).IsRequired().HasColumnName("document_id");
        b.HasIndex(x => x.DocumentId).IsUnique();

        b.Property(x => x.Status).IsRequired().HasColumnName("status")
            .HasConversion<int>();

        b.Property(x => x.CreatedOn).IsRequired().HasColumnName("created_on");
        b.Property(x => x.StartedOn).HasColumnName("started_on");
        b.Property(x => x.CompletedOn).HasColumnName("completed_on");
        b.Property(x => x.ErrorMessage).HasMaxLength(2000).HasColumnName("error_message");
        b.Property(x => x.RetryCount).HasColumnName("retry_count");
        b.Property(x => x.OriginalStorageId).HasMaxLength(200).HasColumnName("original_storage_id");
        b.Property(x => x.ConvertedStorageId).HasMaxLength(200).HasColumnName("converted_storage_id");
        b.Property(x => x.PluginType).HasMaxLength(100).HasColumnName("plugin_type");

        b.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
