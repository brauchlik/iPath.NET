using System.Reflection.Metadata;

namespace iPath.Domain.Entities;

public class ServiceRequest : AuditableEntityWithEvents
{
    public int? ipath2_id { get; set; }

    // public DateTime CreatedOn { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public Guid GroupId { get; set; }
    public Group Group { get; set; }

    /*
    public Guid? RootNodeId { get; set; }
    [JsonIgnore]
    public ServiceRequest? RootNode { get; set; }
    public Guid? ParentNodeId { get; set; }
    */

    public bool IsDraft { get; set; }

    public ICollection<DocumentNode> Documents { get; set; } = [];
    public ICollection<Annotation> Annotations { get; set; } = [];


    // public ICollection<FileUpload> Uploads { get; set; } = [];
    public ICollection<ServiceRequestLastVisit>? LastVisits { get; set; } = [];

    public string NodeType { get; set; } = default!;

    public eNodeVisibility Visibility { get; set; } = eNodeVisibility.GroupMembers;

    public RequestDescription? Description { get; set; } = new();

    public ICollection<QuestionnaireResponseEntity> QuestionnaireResponses { get; set; } = [];

    public ICollection<ServiceRequestUploadFolder> UploadFolders { get; set; } = [];
}


public enum eNodeVisibility
{
    GroupMembers = 0,
    Private = 1,
    Public = 2
}
