namespace iPath.Domain.Entities;

public class ServiceRequestImport : BaseEntity
{
    public Guid ServiceRequestId { get; set; }

    public string? Data { get; set; }
    public string? Info { get; set; }
}


public class DocumentImport : BaseEntity
{
    public Guid DocumentId { get; set; }

    public string? Data { get; set; }
    public string? Info { get; set; }
}