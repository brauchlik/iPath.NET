using iPath.EF.Core.FeatureHandlers.Nodes.Queries;

namespace iPath.EF.Core.FeatureHandlers.Nodes;


public class GetNodeIdListHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetNodeIdListQuery, Task<IReadOnlyList<Guid>>>
{
    public async Task<IReadOnlyList<Guid>> Handle(GetNodeIdListQuery request, CancellationToken cancellationToken)
    {
        // prepare query (only root nodes)
        var q = db.Nodes.AsNoTracking()
            .Where(n => n.GroupId.HasValue);

        if (request.GroupId.HasValue)
        {
            sess.AssertInGroup(request.GroupId.Value);
            q = q.Where(n => n.GroupId.HasValue && n.GroupId.Value == request.GroupId.Value);
        }

        if (request.OwnerId.HasValue)
        {
            q = q.Where(n => n.OwnerId == request.OwnerId.Value);
        }

        // Filter out drafts & private cases
        var spec = new NodeIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null);
        q = q.Where(spec.ToExpression());


        // filter & sort
        q = q.ApplyQuery(request);

        // project
        var projeted = q.Select(n => n.Id);

        // paginate
        return await projeted.ToListAsync(cancellationToken);
    }
}