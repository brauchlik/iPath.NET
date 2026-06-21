using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using iPath.Domain.Entities;

namespace iPath_EFCore.Database.Configurations;

internal class CaseIngestionLineageConfiguration : IEntityTypeConfiguration<CaseIngestionLineage>
{
    public void Configure(EntityTypeBuilder<CaseIngestionLineage> b)
    {
        b.ToTable("case_ingestion_lineages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
    }
}
