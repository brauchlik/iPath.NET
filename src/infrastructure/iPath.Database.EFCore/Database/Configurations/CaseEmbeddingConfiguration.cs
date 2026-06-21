using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using iPath.Domain.Entities;

namespace iPath_EFCore.Database.Configurations;

internal class CaseEmbeddingConfiguration : IEntityTypeConfiguration<CaseEmbedding>
{
    public void Configure(EntityTypeBuilder<CaseEmbedding> b)
    {
        b.ToTable("case_embeddings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
    }
}
