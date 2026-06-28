namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;

public class ScanExternalDocumentsQueryHandler(IRemoteStorageService store, iPathDbContext db, IUserSession sess)
    : IRequestHandler<ScanExternalDocumentsQuery, Task<ScanExternalDocumentResponse>>
{
    public async Task<ScanExternalDocumentResponse> Handle(ScanExternalDocumentsQuery request, CancellationToken ct)
    {
        var serviceRequest = await db.ServiceRequests.FindAsync(request.serviceRequestId, ct);
        Guard.Against.NotFound(request.serviceRequestId, serviceRequest);

        if (!sess.IsAdmin && serviceRequest.OwnerId != sess.User.Id)
            throw new NotAllowedException();

        var folder = await db.ServiceRequestUploadFolders
            .FirstOrDefaultAsync(f => f.ServiceRequestId == request.serviceRequestId, ct);

        if (folder is null)
            return new ScanExternalDocumentResponse(store.ProviderName, []);

        return await store.ScanUploadFolderAsync(folder, ct);
    }
}
