namespace iPath.Application.Features.Nodes;

public record UpdateNodeDescriptionCommand(Guid NodeId, NodeDescription Data)
    : IRequest<UpdateNodeDescriptionCommand, Task<bool>>
    , IEventInput
{
    public string ObjectName => nameof(Node);
}

