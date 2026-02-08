namespace iPath.Application.Features.ServiceRequests.Commands;

public record DeleteServiceRequestUploadFolderCommand(Guid requestId)
    : IRequest<DeleteServiceRequestUploadFolderCommand, Task>;