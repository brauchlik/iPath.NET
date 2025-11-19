namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;

public class UpdateChildNodeSortOrderCommandHandler(iPathDbContext db, IUserSession sess) 
    : IRequestHandler<UpdateChildNodeSortOrderCommand, Task<ChildNodeSortOrderUpdatedEvent>>
{
    public async Task<ChildNodeSortOrderUpdatedEvent> Handle(UpdateChildNodeSortOrderCommand request, CancellationToken ct)
    {
        var node = await db.Nodes
            .Include(n => n.ChildNodes)
            .FirstOrDefaultAsync(n => n.Id == request.NodeId, ct);
        Guard.Against.NotFound(request.NodeId, node);


        await using var tran = await db.Database.BeginTransactionAsync(ct);

        foreach( var child in node.ChildNodes )
        {
            try
            {
                child.SortNr = request.sortOrder[child.Id];
                child.LastModifiedOn = DateTime.UtcNow;
                db.Update(child);
            }
            catch (Exception ex) { }
        }

        var evt = await db.CreateEventAsync<ChildNodeSortOrderUpdatedEvent, UpdateChildNodeSortOrderCommand>(request, node.Id);
        await db.SaveChangesAsync();
        await tran.CommitAsync(ct);

        return evt;
    }
}