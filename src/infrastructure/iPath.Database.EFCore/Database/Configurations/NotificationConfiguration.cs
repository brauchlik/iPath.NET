namespace iPath_EFCore.Database.Configurations;

internal class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).IsRequired();
    }
}
