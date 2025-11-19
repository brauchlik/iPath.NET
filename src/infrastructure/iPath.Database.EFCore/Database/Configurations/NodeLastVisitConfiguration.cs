namespace iPath_EFCore.Database.Configurations;

internal class NodeLastVisitConfiguration : IEntityTypeConfiguration<NodeLastVisit>
{
    public void Configure(EntityTypeBuilder<NodeLastVisit> b)
    {
        b.ToTable("node_lastvisits");
        b.HasKey(x => new { x.UserId, x.NodeId });
        b.HasIndex(x => x.Date);

        b.HasOne(v => v.Node).WithMany(n => n.LastVisits).HasForeignKey(v => v.NodeId);
        b.HasIndex(x => x.NodeId);
    }
}
