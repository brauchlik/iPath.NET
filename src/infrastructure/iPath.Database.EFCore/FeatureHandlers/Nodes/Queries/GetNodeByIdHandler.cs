
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Queries;


public class GetRootNodeByIdQueryHandler(iPathDbContext db, IUserSession sess, ILogger<GetRootNodeByIdQueryHandler> logger)
    : IRequestHandler<GetRootNodeByIdQuery, Task<NodeDto>>
{
    public async Task<NodeDto> Handle(GetRootNodeByIdQuery request, CancellationToken cancellationToken)
    {
        // Direct projection does not work with Sqlite => better call Entities in one query and project in memory
        var node = await db.Nodes.AsNoTracking()
            .Include(n => n.Owner)
            .Include(n => n.ChildNodes).ThenInclude(a => a.Owner)
            .Include(n => n.Annotations).ThenInclude(a => a.Owner)
            .AsSplitQuery()
            .FirstOrDefaultAsync(n => n.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, node);
        Guard.Against.Null(node.GroupId);

        // if not publicly visible, check group
        if (!sess.IsAdmin)
        {
            if (node.Visibility != eNodeVisibility.Public)
            {
                sess.AssertInGroup(node.GroupId.Value);
            }

            var spec = new NodeIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null);
            if (!spec.IsSatisfiedBy(node))
            {
                throw new NotAllowedException($"You are not allowed to access case");
            }
        }

        var dto = node.ToDto();
        return dto;
    }
}
