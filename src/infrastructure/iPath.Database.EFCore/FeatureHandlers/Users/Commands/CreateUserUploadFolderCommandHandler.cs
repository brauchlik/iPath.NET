using FluentResults;
using iPath.Application.Features.Users.Commands;

namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class CreateUserUploadFolderCommandHandler(IRemoteStorageService srv)
    : IRequestHandler<CreateRequestUploadFolderCommand, Task<Guid>>
{
    public async Task<Guid> Handle(CreateRequestUploadFolderCommand request, CancellationToken cancellationToken)
    {
        var res = await srv.CreateUserUploadFolderAsync(request.userId, cancellationToken);
        Guard.Against.Null(res);
        return res.Id;
    }
}
