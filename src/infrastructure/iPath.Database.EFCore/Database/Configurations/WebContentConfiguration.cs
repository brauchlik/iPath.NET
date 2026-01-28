
namespace iPath.EF.Core.Database.Configurations;

public class WebContentConfiguration : IEntityTypeConfiguration<WebContent>
{
    public void Configure(EntityTypeBuilder<WebContent> builder)
    {
        builder.ToTable("webcontent");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .IsRequired(true);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.ChildContent)
            .HasForeignKey(x => x.ParentId)
            .IsRequired(false);
    }
}
