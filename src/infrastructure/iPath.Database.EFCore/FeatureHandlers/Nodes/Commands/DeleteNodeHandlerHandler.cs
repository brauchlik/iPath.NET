namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;


public class DeleteNodeCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<DeleteNodeCommand, Task<NodeDeletedEvent>>
{
    public async Task<NodeDeletedEvent> Handle(DeleteNodeCommand request, CancellationToken ct)
    {
        var node = await db.Nodes.FindAsync(request.NodeId);
        Guard.Against.NotFound(request.NodeId, node);

        if (!sess.IsAdmin())
        {
            // if not admin check if user is owner
            if (node.OwnerId != sess.User.Id)
            {
                // if not , check if user id group moderator
                var groupId = node.GroupId ?? node.RootNode?.GroupId;
                if (groupId.HasValue)
                    sess.AssertInGroup(groupId.Value);
            }
        }

        await using var tran = await db.Database.BeginTransactionAsync(ct);

        if( node.ChildNodes != null)
        {
            foreach (var child in node.ChildNodes)
            {
                db.Nodes.Remove(child);
            }
        }
        db.Nodes.Remove(node);
        var evt = await db.CreateEventAsync<NodeDeletedEvent, DeleteNodeCommand>(request, node.Id);
        await db.SaveChangesAsync(ct);

        await tran.CommitAsync(ct);

        return evt;
    }
}