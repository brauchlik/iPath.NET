
namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;



public class ScanExternalDocumentsQueryHandler(IRemoteStorageService store, iPathDbContext db, IUserSession sess)
    : IRequestHandler<ScanExternalDocumentsQuery, Task<ScanExternalDocumentResponse>>
{
    public async Task<ScanExternalDocumentResponse> Handle(ScanExternalDocumentsQuery request, CancellationToken ct)
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

        return null;
        // return await store.ScanNewFilesAsync(request.serviceRequestId, ct);
    }
}
