using iPath.Application.Features.Users.Commands;

namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class DeleteUserUploadFolderCommandHandler(IRemoteStorageService srv, IUserSession sess)
    : IRequestHandler<DeleteUserUploadFolderCommand, Task>
{
    public async Task Handle(DeleteUserUploadFolderCommand request, CancellationToken cancellationToken)
    {
        // allow only admins and user himself
        if (!sess.IsAdmin)
        {
            if (sess.User.Id != request.userId)
                throw new NotAllowedException();
        }

        await srv.DeleteUserUploadFolderAsync(request.userId, cancellationToken);
    }
}
