using iPath.Application.Features.Documents;

namespace iPath.Application.Features.ServiceRequests;

public class ServiceRequestDto
{
    public string NodeType { get; init; } = default!;
    public int? SortNr { get; set; }

    public Guid Id { get; init; }
    public DateTime CreatedOn { get; set; }

    public Guid OwnerId { get; init; }
    public required OwnerDto Owner { get; init; }

    public Guid? GroupId { get; init; }
    public Guid? RootNodeId { get; init; }
    public Guid? ParentNodeId { get; init; }
    public bool IsDraft { get; init; }

    private RequestDescription? _Description = new();
    public RequestDescription? Description {
        get => _Description;
        init => _Description = value;
    }

    public NodeFile? File { get; init; } = null!;

    public int? ipath2_id { get; init; }

    public ICollection<DocumentDto> Documents { get; init; } = [];
    public ICollection<AnnotationDto> Annotations { get; init; } = [];

    public Guid? UploadFolderId { get; init; }

    public void ResetDescription()
    {
        if (!IsDraft) throw new Exception("Description can be reset on drafts only");
        _Description = new();
    }
}


public static class NodeExtension
{
    public static ServiceRequestDto ToDto(this ServiceRequest node)
    {
        return new ServiceRequestDto
        {
            Id = node.Id,
            NodeType = node.NodeType,
            CreatedOn = node.CreatedOn,
            // SortNr = node.SortNr,
            IsDraft = node.IsDraft,
            OwnerId = node.OwnerId,
            Owner = node.Owner.ToOwnerDto(),
            GroupId = node.GroupId,
            // RootNodeId = node.RootNodeId,
            // ParentNodeId = node.ParentNodeId,
            Description = node.Description,
            // File = node.File,
            ipath2_id = node.ipath2_id,
            Documents = node.Documents is not null ? node.Documents.Select(n => n.ToDto()).ToArray() : [],
            Annotations = node.Annotations is not null ? node.Annotations.Select(a => a.ToDto()).ToArray() : [],
            UploadFolderId = node.UploadFolders.IsEmpty() ? null : node.UploadFolders.First().Id
        };
    }

    public static bool IsSameAs(this ServiceRequest? sr, ServiceRequest? other)
    {
        return sr is not null && other is not null && sr.Id == other.Id;
    }
}
