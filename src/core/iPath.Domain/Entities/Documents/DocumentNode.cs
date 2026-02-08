namespace iPath.Domain.Entities;

public class DocumentNode
     : AuditableEntityWithEvents
{
    public int? ipath2_id { get; set; }

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public Guid ServiceRequestId { get; set; }
    [JsonIgnore]
    public ServiceRequest ServiceRequest { get; set; }

    public int SortNr { get; set; }

    public Guid? ParentNodeId { get; set; }
    [JsonIgnore]
    public DocumentNode? ParentNode { get; set; }
    public ICollection<DocumentNode> ChildNodes { get; set; } = [];

    public ICollection<Annotation> Annotations { get; set; } = [];

    public ICollection<ServiceRequestLastVisit>? LastVisits { get; set; } = [];

    public NodeFile? File { get; set; } = null!;

    public string DocumentType { get; set; } = "";

    public void Delete()
    {
        DeletedOn ??= DateTime.UtcNow;
    }
}
