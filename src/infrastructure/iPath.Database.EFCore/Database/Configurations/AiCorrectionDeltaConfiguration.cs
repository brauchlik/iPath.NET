using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using iPath.Domain.Entities;

namespace iPath_EFCore.Database.Configurations;

internal class AiCorrectionDeltaConfiguration : IEntityTypeConfiguration<AiCorrectionDelta>
{
    public void Configure(EntityTypeBuilder<AiCorrectionDelta> b)
    {
        b.ToTable("ai_correction_deltas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
    }
}
