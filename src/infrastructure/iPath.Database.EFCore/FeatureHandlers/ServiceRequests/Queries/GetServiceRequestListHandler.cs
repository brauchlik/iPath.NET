namespace iPath.EF.Core.FeatureHandlers.ServiceRequests;

using iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;
using EF = Microsoft.EntityFrameworkCore.EF;

public class GetNodesQueryHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetServiceRequestListQuery, Task<PagedResultList<ServiceRequestListDto>>>
{
    public async Task<PagedResultList<ServiceRequestListDto>> Handle(GetServiceRequestListQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(sess.User);

        // prepare query (only root nodes)
        var q = db.ServiceRequests.AsNoTracking();
        q = q.ApplyRequest(request, sess);

        // project
        IQueryable<ServiceRequestListDto> projected;
        if (!request.IncludeDetails)
        {
            projected = q.ProjectToList();
        }
        else
        {
            projected = q.ProjectToListDetails(sess.User.Id);
        }

        // paginate
        return await projected.ToPagedResultAsync(request, cancellationToken);
    }
}