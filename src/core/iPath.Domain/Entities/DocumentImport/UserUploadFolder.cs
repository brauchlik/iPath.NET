namespace iPath.Domain.Entities;

public class UserUploadFolder : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string StorageProvider { get; set; }
    public string StorageId { get; set; }

    public ICollection<ServiceRequestUploadFolder> RequestUploadFolders { get; set; } = [];

    private UserUploadFolder()
    {
    }

    public static UserUploadFolder Create(Guid userId,  string storageProvider, string storageId)
    {
        return new UserUploadFolder
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            StorageProvider = storageProvider,
            StorageId = storageId
        };
    }
}
