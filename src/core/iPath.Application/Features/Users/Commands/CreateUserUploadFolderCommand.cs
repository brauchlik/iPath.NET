namespace iPath.Application.Features.Users.Commands;

public record CreateRequestUploadFolderCommand(Guid userId)
    : IRequest<CreateRequestUploadFolderCommand, Task<Guid>>;
