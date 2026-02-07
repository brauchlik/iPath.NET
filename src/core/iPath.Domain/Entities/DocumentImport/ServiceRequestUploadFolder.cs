namespace iPath.Domain.Entities;

public class ServiceRequestUploadFolder : BaseEntity
{
    public Guid UploadFolderId { get; set; }
    public UserUploadFolder UploadFolder { get; set; }

    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
