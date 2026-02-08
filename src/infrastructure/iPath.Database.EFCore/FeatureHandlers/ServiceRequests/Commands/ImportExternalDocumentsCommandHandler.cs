
namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Commands;

public class ImportExternalDocumentsCommandHandler(IRemoteStorageService store, iPathDbContext db, IUserSession sess)
    : IRequestHandler<ImportExternalDocumentsCommand, Task<int>>
{
    public async Task<int> Handle(ImportExternalDocumentsCommand request, CancellationToken ct)
    {
        // only owner or admins
        var folder = await db.ServiceRequestUploadFolders
            .AsNoTracking()
            .Include(f => f.ServiceRequest).ThenInclude(sr => sr.Documents)
            .SingleOrDefaultAsync(x => x.Id == request.uploadFolderId, ct);
        Guard.Against.NotFound(request.uploadFolderId, folder);

        if (!sess.IsAdmin)
        {
            if (folder.ServiceRequest.OwnerId != sess.User.Id)
            {
                throw new NotAllowedException();
            }
        }

        return await store.ImportUploadFolderAsync(folder, request.storgeIds, ct);
    }
}
