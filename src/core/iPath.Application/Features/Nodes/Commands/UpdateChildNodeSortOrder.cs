namespace iPath.Application.Features.Nodes;

public record UpdateChildNodeSortOrderCommand(Guid NodeId, Dictionary<Guid, int> sortOrder)
    : IRequest<UpdateChildNodeSortOrderCommand, Task<ChildNodeSortOrderUpdatedEvent>>
    , IEventInput
{
    public string ObjectName => nameof(Node);
}

