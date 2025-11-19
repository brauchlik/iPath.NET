using Microsoft.EntityFrameworkCore.Sqlite;

namespace iPath_EFCore.Database.Configurations;

internal class CommunityConfiguration : IEntityTypeConfiguration<Community>
{
    public void Configure(EntityTypeBuilder<Community> b)
    {
        // b.ToTable("communities");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200);

        b.Property(x => x.Description).HasMaxLength(500);

        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).IsRequired();

        b.HasMany(x => x.Groups).WithOne(g => g.Community).HasForeignKey(g => g.CommunityId);
        b.HasMany(x => x.Members).WithOne(m => m.Community).HasForeignKey(m => m.CommunityId).OnDelete(DeleteBehavior.NoAction);
    }
}
