namespace iPath.Application.Features.Nodes;


public record CreateNodeCommand(Guid GroupId, string NodeType, NodeDescription? Description = null, Guid? NodeId = null)
    : IRequest<CreateNodeCommand, Task<NodeDto>>
    , IEventInput
{
    public string ObjectName => nameof(Node);
}


public static partial class NodeCommandExtensions
{
    public static Node CreateNode(CreateNodeCommand cmd, Guid userId)
    {
        var node = new Node
        {
            Id = cmd.NodeId.HasValue ? cmd.NodeId.Value : Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
            GroupId = cmd.GroupId,
            OwnerId = userId,
            Description = cmd.Description ?? new(),
            NodeType = cmd.NodeType,
            IsDraft = true
        };

        node.CreateEvent<RootNodeCreatedEvent, CreateNodeCommand>(cmd, userId);
        return node;
    }
}

