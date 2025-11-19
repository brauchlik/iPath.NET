namespace iPath.EF.Core.FeatureHandlers.Nodes;


public class GetNodesQueryHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetNodesQuery, Task<PagedResultList<NodeListDto>>>
{
    public async Task<PagedResultList<NodeListDto>> Handle(GetNodesQuery request, CancellationToken cancellationToken)
    {
        // prepare query
        var q = db.Nodes.AsNoTracking();

        if (request.GroupId.HasValue)
        {
            sess.AssertInGroup(request.GroupId.Value);
            q = q.Where(n => n.GroupId.HasValue && n.GroupId.Value == request.GroupId.Value);
        }

        if (request.OwnerId.HasValue)
        {
            q = q.Where(n => n.OwnerId == request.OwnerId.Value);
        }

        // filter & sort
        q = q.ApplyQuery(request);

        // project
        var projeted = q.Select(n => new NodeListDto
        {
            Id = n.Id,
            NodeType = n.NodeType,
            CreatedOn = n.CreatedOn,
            OwnerId = n.OwnerId,
            Owner = new OwnerDto(n.OwnerId, n.Owner.UserName),
            GroupId = n.GroupId,
            Description = n.Description,
            AnnotationCount = n.Annotations.Count()
        });

        // paginate
        return await projeted.ToPagedResultAsync(request, cancellationToken);
    }
}