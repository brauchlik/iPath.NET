using FluentResults;
using iPath.Application.Features.Users.Commands;

namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class CreateUserUploadFolderCommandHandler(IRemoteStorageService srv, IUserSession sess)
    : IRequestHandler<CreateRequestUploadFolderCommand, Task<Guid>>
{
    public async Task<Guid> Handle(CreateRequestUploadFolderCommand request, CancellationToken cancellationToken)
    {
        // allow only admins and user himself
        if (!sess.IsAdmin)
        {
            if (sess.User.Id != request.userId)
                throw new NotAllowedException();
        }

        var res = await srv.CreateUserUploadFolderAsync(request.userId, cancellationToken);
        Guard.Against.Null(res);
        return res.Id;
    }
}
