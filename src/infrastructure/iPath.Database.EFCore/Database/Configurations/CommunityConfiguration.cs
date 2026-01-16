namespace iPath_EFCore.Database.Configurations;

internal class CommunityConfiguration : IEntityTypeConfiguration<Community>
{
    public void Configure(EntityTypeBuilder<Community> b)
    {
        b.ToTable("communities");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.Name).HasMaxLength(200).HasColumnName("name");

        b.ComplexProperty(x => x.Settings, pb => pb.ToJson());

        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.Groups).WithOne(g => g.Community).HasForeignKey(g => g.CommunityId);
        b.HasMany(x => x.ExtraGroups).WithOne(g => g.Community).HasForeignKey(g => g.CommunityId);
        b.HasMany(x => x.Members).WithOne(m => m.Community).HasForeignKey(m => m.CommunityId).OnDelete(DeleteBehavior.NoAction);

        b.HasMany(g => g.Quesionnaires).WithOne(g => g.Community).HasForeignKey(g => g.CommunityId).IsRequired(true);
    }
}
