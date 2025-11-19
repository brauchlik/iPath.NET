namespace iPath.Application.Features.Nodes;

public record CreateNodeCommand(Guid GroupId, string NodeType, NodeDescription Description, Guid? NodeId = null)
    : IRequest<CreateNodeCommand, Task<NodeListDto>>
    , IEventInput
{
    public string ObjectName => nameof(Node);
}
