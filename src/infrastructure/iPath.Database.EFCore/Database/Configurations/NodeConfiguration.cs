namespace iPath_EFCore.Database.Configurations;

internal class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> b)
    {
        b.ToTable("nodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.OwnerId).IsRequired().HasColumnName("owner_id");
        b.Property(b => b.GroupId).HasColumnName("group_id");
        b.HasIndex(b => b.GroupId);

        b.HasOne(x => x.Owner).WithMany(u => u.OwnedNodes).HasForeignKey(x => x.OwnerId).IsRequired();
        b.HasOne(x => x.Group).WithMany(g => g.Nodes).HasForeignKey(x => x.GroupId).IsRequired(false);

        b.HasMany(x => x.ChildNodes).WithOne(c => c.RootNode).HasForeignKey(c => c.RootNodeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.RootNode).WithMany(r => r.ChildNodes).HasForeignKey(c => c.RootNodeId).OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.Annotations).WithOne(a => a.Node).HasForeignKey(a => a.NodeId).OnDelete(DeleteBehavior.Cascade);

        // b.HasMany(x => x.Uploads).WithOne(f => f.Node).HasForeignKey(x => x.NodeId).OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.QuestionnaireResponses).WithOne(r => r.Node).HasForeignKey(r => r.NodeId).IsRequired(false);

        b.ComplexProperty(x => x.Description, pb => pb.ToJson("description"));
        b.ComplexProperty(x => x.File, pb => pb.ToJson("file"));
    }
}
