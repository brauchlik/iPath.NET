using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace iPath_EFCore.Database.Configurations;

internal class DocumentNodeConfiguration : IEntityTypeConfiguration<DocumentNode>
{
    public void Configure(EntityTypeBuilder<DocumentNode> b)
    {
        b.ToTable("documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.OwnerId).IsRequired().HasColumnName("owner_id");
        b.Property(b => b.ServiceRequestId).HasColumnName("servicerequest_id");
        b.HasIndex(b => b.ServiceRequestId).IsUnique(false);

        b.HasMany(x => x.ChildNodes).WithOne(c => c.ParentNode).HasForeignKey(c => c.ParentNodeId).OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Annotations).WithOne(a => a.Document).HasForeignKey(a => a.DcoumentNodeId).OnDelete(DeleteBehavior.Cascade);

        b.ComplexProperty(x => x.File, pb => pb.ToJson("file"));

        b.HasQueryFilter(x => !x.DeletedOn.HasValue);
    }
}
