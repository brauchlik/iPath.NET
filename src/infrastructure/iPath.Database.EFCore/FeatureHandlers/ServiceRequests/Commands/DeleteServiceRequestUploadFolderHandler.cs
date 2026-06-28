using iPath.Application.Features.ServiceRequests.Commands;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Commands;

public class DeleteServiceRequestUploadFolderHandler(iPathDbContext db, IRemoteStorageService storage, IUserSession sess)
    : IRequestHandler<DeleteServiceRequestUploadFolderCommand, Task>
{
    public async Task Handle(DeleteServiceRequestUploadFolderCommand request, CancellationToken ct)
    {
        var sr = await db.ServiceRequests.FindAsync(request.requestId, ct);
        Guard.Against.NotFound(request.requestId, sr);

        if (!sess.IsAdmin && sr.OwnerId != sess.User.Id)
            throw new NotAllowedException();

        var folder = await db.ServiceRequestUploadFolders
            .FirstOrDefaultAsync(f => f.ServiceRequestId == request.requestId, ct);

        if (folder is not null)
        {
            await storage.DeleteRequestUploadFolderAsync(folder.Id, ct);
            db.ServiceRequestUploadFolders.Remove(folder);
            await db.SaveChangesAsync(ct);
        }
    }
}
