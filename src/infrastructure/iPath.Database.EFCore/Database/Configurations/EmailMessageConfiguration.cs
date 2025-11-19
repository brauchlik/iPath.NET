namespace iPath.EF.Core.Database.Configurations;

internal class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> b)
    {
        b.ToTable("temp_emails");
        b.HasKey(e => e.Id);
    }
}
