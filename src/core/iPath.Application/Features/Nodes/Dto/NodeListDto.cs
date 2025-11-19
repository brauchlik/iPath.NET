namespace iPath.Application.Features.Nodes;

public record NodeListDto
{
    public Guid Id { get; init; }
    public string NodeType { get; init; } = default!;
    public DateTime CreatedOn { get; set; }

    public Guid OwnerId { get; init; }
    public required OwnerDto Owner { get; init; }

    public Guid? GroupId { get; init; }

    public NodeDescription? Description { get; init; } = new();

    public int? AnnotationCount { get; init; }
}


public static class NodeListExtension
{
    public static NodeListDto ToListDto(this Node node)
    {
        return new NodeListDto
        {
            Id = node.Id,
            NodeType = node.NodeType,
            CreatedOn = node.CreatedOn,
            OwnerId = node.OwnerId,
            Owner = node.Owner.ToOwnerDto(),
            GroupId = node.GroupId,
            Description = node.Description,
            AnnotationCount = node.Annotations?.Count
        };
    }
}