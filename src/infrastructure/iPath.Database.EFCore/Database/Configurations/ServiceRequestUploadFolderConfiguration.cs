
namespace iPath.EF.Core.Database.Configurations;

public class ServiceRequestUploadFolderConfiguration : IEntityTypeConfiguration<ServiceRequestUploadFolder>
{
    public void Configure(EntityTypeBuilder<ServiceRequestUploadFolder> builder)
    {
        builder.ToTable("servicerequestuploadfolder");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.ServiceRequest).WithMany(s => s.UploadFolders)
            .HasForeignKey(x => x.ServiceRequestId)
            .IsRequired(true);
        builder.HasOne(x => x.UploadFolder).WithMany(u => u.RequestUploadFolders)
            .HasForeignKey(x => x.UploadFolderId)
            .IsRequired(true);
    }
}
