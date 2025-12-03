
using DispatchR;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;


public class UpdateNodeHandler(iPathDbContext db, IMediator mediator, IUserSession sess)
    : IRequestHandler<UpdateNodeCommand, Task<bool>>
{
    public async Task<bool> Handle(UpdateNodeCommand request, CancellationToken ct)
    {
        var node = await db.Nodes
            .Include(n => n.RootNode)
            .SingleOrDefaultAsync(n => n.Id == request.NodeId, ct);
        Guard.Against.NotFound(request.NodeId, node);

        // permission
        if (!sess.IsAdmin)
        {
            var gid = node.GroupId.HasValue ? node.GroupId.Value : node.RootNode?.GroupId;
            if (!gid.HasValue || !sess.IsGroupModerator(gid.Value))
            {
                if (node.OwnerId != sess.User.Id)
                {
                    throw new NotAllowedException();
                }
            }
        }

        node.UpdateNode(request, sess.User.Id);
        await db.SaveChangesAsync(ct);

        // update user NodeVisit
        await mediator.Send(new UpdateNodeVisitCommand(node.Id), ct);

        return true;
    }
}