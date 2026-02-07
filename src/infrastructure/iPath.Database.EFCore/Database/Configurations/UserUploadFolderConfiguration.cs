
namespace iPath.EF.Core.Database.Configurations;

public class UserUploadFolderConfiguration : IEntityTypeConfiguration<UserUploadFolder>
{
    public void Configure(EntityTypeBuilder<UserUploadFolder> builder)
    {
        builder.ToTable("useruploadfolder");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.User).WithMany(u => u.UploadFolders).HasForeignKey(x => x.UserId).IsRequired(true);
    }
}
