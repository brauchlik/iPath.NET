namespace iPath.Application.Features.Users.Commands;

public record DeleteUserUploadFolderCommand(Guid userId)
    : IRequest<DeleteUserUploadFolderCommand, Task>;