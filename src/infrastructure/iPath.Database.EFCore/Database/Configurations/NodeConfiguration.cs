namespace iPath_EFCore.Database.Configurations;

internal class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> b)
    {
        b.ToTable("nodes");
        b.HasKey(x => x.Id);

        b.Property(x => x.OwnerId).IsRequired();
        b.HasIndex(b => b.GroupId);

        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).IsRequired();
        b.HasOne(x => x.Group).WithMany(g => g.Nodes).HasForeignKey(x => x.GroupId).IsRequired(false);

        b.HasMany(x => x.ChildNodes).WithOne(c => c.RootNode).HasForeignKey(c => c.RootNodeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.RootNode).WithMany(r => r.ChildNodes).HasForeignKey(c => c.RootNodeId).OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.Annotations).WithOne(a => a.Node).HasForeignKey(a => a.NodeId).OnDelete(DeleteBehavior.Cascade);

        // b.HasMany(x => x.Uploads).WithOne(f => f.Node).HasForeignKey(x => x.NodeId).OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.QuestionnaireResponses).WithOne(r => r.Node).HasForeignKey(r => r.NodeId).IsRequired(false);

        b.OwnsOne(x => x.Description, pb => pb.ToJson());
        b.OwnsOne(x => x.File, pb => pb.ToJson());
    }
}
