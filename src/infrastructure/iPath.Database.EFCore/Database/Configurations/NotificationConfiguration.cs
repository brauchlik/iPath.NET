using iPath.Domain.Entities.Mails;

namespace iPath_EFCore.Database.Configurations;

internal class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).IsRequired();
    }
}
