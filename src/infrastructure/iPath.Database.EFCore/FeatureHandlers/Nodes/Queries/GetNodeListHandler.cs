namespace iPath.EF.Core.FeatureHandlers.Nodes;

using iPath.EF.Core.FeatureHandlers.Nodes.Queries;
using EF = Microsoft.EntityFrameworkCore.EF;

public class GetNodesQueryHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetNodesQuery, Task<PagedResultList<NodeListDto>>>
{
    public async Task<PagedResultList<NodeListDto>> Handle(GetNodesQuery request, CancellationToken cancellationToken)
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

        // freetext search
        if (!string.IsNullOrEmpty(request.SearchString))
        {
            q = q.ApplySearchString(request.SearchString); 
        }

        if (request.Filter is not null)
        {
            foreach (var f in request.Filter)
            {
                q = q.ApplyNodeFilter(f);
            }
        }

        // Filter out drafts & private cases
        var spec = new NodeIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null);
        q = q.Where(spec.ToExpression());


        // filter & sort
        q = q.ApplyQuery(request);

        // project
        IQueryable<NodeListDto> projected;
        if (!request.IncludeDetails)
        {
            projected = q.Select(n => new NodeListDto
            {
                Id = n.Id,
                NodeType = n.NodeType,
                CreatedOn = n.CreatedOn,
                IsDraft = n.IsDraft,
                OwnerId = n.OwnerId,
                Owner = new OwnerDto(n.OwnerId, n.Owner.UserName, n.Owner.Email),
                GroupId = n.GroupId,
                Description = n.Description
            });
        }
        else
        {
            projected = q.Select(n => new NodeListDto
            {
                Id = n.Id,
                NodeType = n.NodeType,
                CreatedOn = n.CreatedOn,
                IsDraft = n.IsDraft,
                OwnerId = n.OwnerId,
                Owner = new OwnerDto(n.OwnerId, n.Owner.UserName, n.Owner.Email),
                GroupId = n.GroupId,
                Description = n.Description,
                AnnotationCount = n.Annotations.Count(),
                LastAnnotationDate = n.Annotations.Max(a => a.CreatedOn),
                LastVisit = n.LastVisits.Where(v => v.UserId == sess.User.Id).Max(v => v.Date)
            });
        }

        // paginate
        return await projected.ToPagedResultAsync(request, cancellationToken);
    }
}