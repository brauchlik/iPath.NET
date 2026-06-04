namespace iPath.Application.Features.ServiceRequests;

public record ServiceRequestListDto
{
    public Guid Id { get; init; }
    public string NodeType { get; init; } = default!;
    public DateTime CreatedOn { get; init; }
    public bool IsDraft { get; init; }

    public Guid OwnerId { get; init; }
    public required OwnerDto Owner { get; init; }

    public Guid? GroupId { get; init; }

    public RequestDescription? Description { get; init; } = new();

    public int? AnnotationCount { get; init; }

    public DateTime? LastVisit { get; set; }
    public DateTime? LastAnnotationDate { get; set; }

}


public static class NodeListExtension
{
    public static ServiceRequestListDto ToListDto(this ServiceRequest node)
    {
        return new ServiceRequestListDto
        {
            Id = node.Id,
            NodeType = node.NodeType,
            CreatedOn = node.CreatedOn,
            IsDraft = node.IsDraft,
            OwnerId = node.OwnerId,
            Owner = node.Owner.ToOwnerDto(),
            GroupId = node.GroupId,
            Description = node.Description,
            AnnotationCount = node.Annotations?.Count,
            LastAnnotationDate = node.Annotations is not null && node.Annotations.Count > 0
                ? node.Annotations.Max(x => x.CreatedOn) : null,
            LastVisit = node.LastVisits is not null && node.LastVisits.Count > 0
                ? node.LastVisits.Max(x => x.Date) : null
        };
    }
}