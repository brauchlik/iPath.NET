namespace iPath_EFCore.Database.Configurations;

internal class CommunityMemberConfiguration : IEntityTypeConfiguration<CommunityMember>
{
    void IEntityTypeConfiguration<CommunityMember>.Configure(EntityTypeBuilder<CommunityMember> b)
    {
        b.ToTable("community_group_members");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.UserId).HasColumnName("user_id");
        b.HasOne(x => x.User).WithMany(u => u.CommunityMembership).HasForeignKey(x => x.UserId).IsRequired()
            .OnDelete(DeleteBehavior.ClientCascade);

        b.Property(x => x.CommunityId).HasColumnName("community_id");
        b.HasOne(x => x.Community).WithMany(c => c.Members).HasForeignKey(x => x.CommunityId).IsRequired()
            .OnDelete(DeleteBehavior.ClientCascade);

        b.HasIndex(builder => new { builder.UserId, builder.CommunityId }).IsUnique();
    }
}
