namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;


public class UpdateNodeDescriptionCommandHandler(iPathDbContext db, IUserSession sess) 
    : IRequestHandler<UpdateNodeDescriptionCommand, Task<bool>>
{
    public async Task<bool> Handle(UpdateNodeDescriptionCommand request, CancellationToken ct)
    {      
        var node = await db.Nodes.FindAsync(request.NodeId, ct);
        Guard.Against.NotFound(request.NodeId, node);

        await using var tran = await db.Database.BeginTransactionAsync(ct);

        node.Description = request.Data;
        node.LastModifiedOn = DateTime.UtcNow;
        var evt = await db.CreateEventAsync<NodeDescriptionUpdatedEvent, UpdateNodeDescriptionCommand>(request, node.Id);
        await db.SaveChangesAsync(ct);
        await tran.CommitAsync(ct);

        return true;
    }
}