namespace iPath_EFCore.Database.Configurations;

internal class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> b)
    {
        b.ToTable("group_members");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Group).WithMany(g => g.Members).HasForeignKey(x => x.GroupId).IsRequired();
        b.HasOne(x => x.User).WithMany(u => u.GroupMembership).HasForeignKey(x => x.UserId).IsRequired();

        b.HasIndex(builder => new { builder.UserId, builder.GroupId }).IsUnique();
    }
}
