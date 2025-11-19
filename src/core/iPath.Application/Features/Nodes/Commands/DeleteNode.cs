namespace iPath.Application.Features.Nodes;

public record DeleteNodeCommand(Guid NodeId)
    : IRequest<DeleteNodeCommand, Task<NodeDeletedEvent>>
    , IEventInput
{
    public string ObjectName => nameof(Node);
}

