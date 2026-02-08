using iPath.Application.Features.ServiceRequests.Commands;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Commands;

public class CreateServiceRequestUploadFolderHandler(iPathDbContext db, IRemoteStorageService storage, IUserSession sess)
    : IRequestHandler<CreateServiceRequestUploadFolderCommand, Task<Guid>>

{
    public async Task<Guid> Handle(CreateServiceRequestUploadFolderCommand request, CancellationToken ct)
    {
        var sr = await db.ServiceRequests.FindAsync(request.requestId, ct);
        Guard.Against.NotFound(request.requestId, sr);
        if (sr.OwnerId != sess.User.Id)
        {
            throw new NotAllowedException();
        }

        var f = await storage.CreateRequestUploadFolderAsync(sr.Id, sess.User.Id, ct);
        return f.Id;
    }
}
