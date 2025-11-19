namespace iPath.Application.Features.Nodes;

public record NodeDto
{
    public Guid Id { get; init; }
    public string NodeType { get; init; } = default!;
    public DateTime CreatedOn { get; set; }
    public int? SortNr { get; set; }

    public Guid OwnerId { get; init; }
    public required OwnerDto Owner { get; init; }

    public Guid? GroupId { get; init; }
    public Guid? RootNodeId { get; init; }
    public Guid? ParentNodeId { get; init; }

    public NodeDescription? Description { get; init; } = new();
    public NodeFile? File { get; init; } = null!;

    public int? ipath2_id { get; init; }

    public ICollection<NodeDto> ChildNodes { get; init; } = [];
    public ICollection<AnnotationDto> Annotations { get; init; } = [];
}


public static class NodeExtension
{
    public static NodeDto ToDto(this Node node)
    {
        return new NodeDto
        {
            Id = node.Id,
            NodeType = node.NodeType,
            CreatedOn = node.CreatedOn,
            SortNr = node.SortNr,
            OwnerId = node.OwnerId,
            Owner = node.Owner.ToOwnerDto(),
            GroupId = node.GroupId,
            RootNodeId = node.RootNodeId,
            ParentNodeId = node.ParentNodeId,
            Description = node.Description,
            File = node.File,
            ipath2_id = node.ipath2_id,
            ChildNodes = node.ChildNodes is not null ? node.ChildNodes.Select(n => n.ToDto()).ToArray() : [],
            Annotations = node.Annotations is not null ? node.Annotations.Select(a => a.ToDto()).ToArray() : []
        };
    }
}
