using iPath.Application.Features.Users.Commands;

namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class DeleteUserUploadFolderCommandHandler(IRemoteStorageService srv)
    : IRequestHandler<DeleteUserUploadFolderCommand, Task>
{
    public async Task Handle(DeleteUserUploadFolderCommand request, CancellationToken cancellationToken)
    {
        await srv.DeleteUserUploadFolderAsync(request.userId, cancellationToken);
    }
}
