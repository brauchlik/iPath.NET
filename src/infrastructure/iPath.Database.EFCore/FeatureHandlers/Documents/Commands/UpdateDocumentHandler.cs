using iPath.Application.Features.Documents;

namespace iPath.EF.Core.FeatureHandlers.Documents;

public class UpdateDocumentHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<UpdateDocumenttCommand, Task>
{
    public async Task Handle(UpdateDocumenttCommand request, CancellationToken ct)
    {
        var document = await db.Documents
            .Include(d => d.ServiceRequest)
            .SingleOrDefaultAsync(d => d.Id == request.DocumentId, ct);
        Guard.Against.NotFound(request.DocumentId, document);

        if (!sess.IsAdmin)
        {
            // if not admin check if user is owner
            if (document.ServiceRequest.OwnerId != sess.User.Id)
            {
                sess.AssertInGroup(document.ServiceRequest.GroupId);
            }
        }

        if (request.Description is not null)
        {
            // Where the file is stored is server-owned: a client's (possibly stale) copy must never
            // undo a storage migration or point the document at another object.
            var file = request.Description.Clone();
            file.Storage = document.File.Storage;
            file.RetiredLocations = [.. document.File.RetiredLocations];
            document.File = file;
        }

        await db.SaveChangesAsync(ct);
    }
}
