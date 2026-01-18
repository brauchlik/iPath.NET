using Microsoft.Identity.Client.Extensions.Msal;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;


public class DeleteServiceRequestCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<DeleteServiceRequestCommand, Task<ServiceRequestDeletedEvent>>
{
    public async Task<ServiceRequestDeletedEvent> Handle(DeleteServiceRequestCommand request, CancellationToken ct)
    {
        var node = await db.ServiceRequests
            .Include(s => s.Documents)
            .SingleOrDefaultAsync(r => r.Id == request.NodeId, ct);
        Guard.Against.NotFound(request.NodeId, node);

        if (!sess.IsAdmin)
        {
            // if not admin check if user is owner
            if (node.OwnerId != sess.User.Id)
            {
                // if not , check if user id group moderator
                if (node.GroupId.HasValue)
                    sess.AssertInGroup(node.GroupId.Value);
            }
        }

        await using var tran = await db.Database.BeginTransactionAsync(ct);

        if( node.Documents != null)
        {
            foreach (var doc in node.Documents)
            {
                doc.Delete();
                db.Documents.Remove(doc);
            }
        }
        db.ServiceRequests.Remove(node);
        var evt = await db.CreateEventAsync<ServiceRequestDeletedEvent, DeleteServiceRequestCommand>(request, node.Id, sess);
        await db.SaveChangesAsync(ct);

        await tran.CommitAsync(ct);

        return evt;
    }
}