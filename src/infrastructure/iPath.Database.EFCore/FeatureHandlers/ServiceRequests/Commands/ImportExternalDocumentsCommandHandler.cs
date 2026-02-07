
namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Commands;

public class ImportExternalDocumentsCommandHandler(IRemoteStorageService store, iPathDbContext db, IUserSession sess)
    : IRequestHandler<ImportExternalDocumentsCommand, Task>
{
    public async Task Handle(ImportExternalDocumentsCommand request, CancellationToken ct)
    {
        // only owner or admins
        var serviceRequest = await db.ServiceRequests.FindAsync(request.serviceRequestId, ct);
        Guard.Against.NotFound(request.serviceRequestId, serviceRequest);

        if (!sess.IsAdmin)
        {
            if (serviceRequest.OwnerId != sess.User.Id)
            {
                throw new NotAllowedException();
            }
        }

        // await store.ImportNewFilesAsync(request.serviceRequestId, request.storgeIds, ct);
    }
}
