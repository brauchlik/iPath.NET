namespace iPath_EFCore.Database.Configurations;

internal class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> b)
    {
        b.ToTable("groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.Name).HasMaxLength(200);

        b.HasOne(g => g.Owner).WithMany().HasForeignKey(g => g.OwnerId).IsRequired(false);

        b.HasMany(x => x.Communities).WithOne(x => x.Group).IsRequired(true);

        b.HasMany(x => x.Members).WithOne(m => m.Group).HasForeignKey(m => m.GroupId);
        b.HasMany(x => x.Nodes).WithOne(n => n.Group).HasForeignKey(n => n.GroupId).IsRequired();

        b.ComplexProperty(x => x.Settings, pb => pb.ToJson());

        b.HasMany(g => g.Quesionnaires).WithOne(g => g.Group).HasForeignKey(g => g.GroupId).IsRequired(true);
    }
}
