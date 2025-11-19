namespace iPath_EFCore.Database.Configurations;

internal class CommunityMemberConfiguration : IEntityTypeConfiguration<CommunityMember>
{
    void IEntityTypeConfiguration<CommunityMember>.Configure(EntityTypeBuilder<CommunityMember> b)
    {
        // b.ToTable("community_group_members");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Community).WithMany(c => c.Members).HasForeignKey(x => x.CommunityId).IsRequired();
        b.HasOne(x => x.User).WithMany(u => u.CommunityMembership).HasForeignKey(x => x.UserId).IsRequired();

        b.HasIndex(builder => new { builder.UserId, builder.CommunityId }).IsUnique();
    }
}
