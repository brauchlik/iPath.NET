namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;

public class UpdateChildNodeSortOrderCommandHandler(iPathDbContext db, IUserSession sess) 
    : IRequestHandler<UpdateDocumentsSortOrderCommand, Task<ChildNodeSortOrderUpdatedEvent>>
{
    public async Task<ChildNodeSortOrderUpdatedEvent> Handle(UpdateDocumentsSortOrderCommand request, CancellationToken ct)
    {
        var node = await db.ServiceRequests
            .Include(n => n.Documents)
            .FirstOrDefaultAsync(n => n.Id == request.NodeId, ct);
        Guard.Against.NotFound(request.NodeId, node);


        await using var tran = await db.Database.BeginTransactionAsync(ct);

        foreach( var child in node.Documents )
        {
            try
            {
                child.SortNr = request.sortOrder[child.Id];
                child.LastModifiedOn = DateTime.UtcNow;
                db.Update(child);
            }
            catch (Exception ex) { }
        }

        var evt = await db.CreateEventAsync<ChildNodeSortOrderUpdatedEvent, UpdateDocumentsSortOrderCommand>(request, node.Id, sess);
        await db.SaveChangesAsync();
        await tran.CommitAsync(ct);

        return evt;
    }
}