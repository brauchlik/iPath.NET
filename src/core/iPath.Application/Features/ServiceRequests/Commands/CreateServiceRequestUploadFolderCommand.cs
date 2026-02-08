namespace iPath.Application.Features.ServiceRequests.Commands;

public record CreateServiceRequestUploadFolderCommand(Guid requestId)
    : IRequest<CreateServiceRequestUploadFolderCommand, Task<Guid>>;
