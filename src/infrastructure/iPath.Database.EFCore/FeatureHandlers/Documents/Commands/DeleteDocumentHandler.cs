using iPath.Application.Features.Documents;

namespace iPath.EF.Core.FeatureHandlers.Documents;

public class DeleteDocumentHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<DeleteDocumentCommand, Task>
{
    public async Task Handle(DeleteDocumentCommand request, CancellationToken ct)
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

        document.Delete();
        await db.SaveChangesAsync(ct);
    }
}
