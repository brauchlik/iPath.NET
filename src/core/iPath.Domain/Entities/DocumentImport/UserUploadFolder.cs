namespace iPath.Domain.Entities;

public class UserUploadFolder : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    public string StorageProvider { get; set; }
    public string StorageId { get; set; }
}
