namespace iPath.Domain.Entities;

public class ServiceRequestUploadFolder : BaseEntity
{
    public Guid UploadFolderId { get; set; }
    public UserUploadFolder UploadFolder { get; set; }

    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; }

    public string StorageId { get; set; }


    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    private ServiceRequestUploadFolder()
    {   
    }

    public static ServiceRequestUploadFolder Create(Guid uploadFolderId, Guid serviceRequestId, string storageId)
    {
        return new ServiceRequestUploadFolder
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            UploadFolderId = uploadFolderId,
            ServiceRequestId = serviceRequestId,
            StorageId = storageId
        };
    }
}
