namespace iPath.Application.Features.ServiceRequests.Commands;

public record SyncServiceRequestToStorageCommand(Guid? ServiceRequestId = null, bool AllDocuments = false, Guid? DocumentId = null)
    : IRequest<SyncServiceRequestToStorageCommand, Task>;
