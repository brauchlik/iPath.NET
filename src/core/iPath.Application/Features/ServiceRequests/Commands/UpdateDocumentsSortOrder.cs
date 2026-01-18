namespace iPath.Application.Features.ServiceRequests;

public record UpdateDocumentsSortOrderCommand(Guid NodeId, Dictionary<Guid, int> sortOrder)
    : IRequest<UpdateDocumentsSortOrderCommand, Task<ChildNodeSortOrderUpdatedEvent>>
    , IEventInput
{
    public string ObjectName => nameof(ServiceRequest);
}

