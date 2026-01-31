namespace iPath.EF.Core.FeatureHandlers.ServiceRequests;

using iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;
using EF = Microsoft.EntityFrameworkCore.EF;

public class GetNodesQueryHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetServiceRequestsQuery, Task<PagedResultList<ServiceRequestListDto>>>
{
    public async Task<PagedResultList<ServiceRequestListDto>> Handle(GetServiceRequestsQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(sess.User);

        // prepare query (only root nodes)
        var q = db.ServiceRequests.AsNoTracking();

        var spec = Specification<ServiceRequest>.All;
        
        if (request.RequestFilter == eRequestFilter.Group && request.GroupId.HasValue)
        {
            sess.AssertInGroup(request.GroupId.Value);
            spec = spec.And(new ServiceRequestIsInGroupSpecifications(request.GroupId.Value));
            // q = q.Where(n => n.GroupId == request.GroupId.Value);
        }

        if (request.CommunityId.HasValue)
        {
            spec = spec.And(new ServiceRequestIsInCommunitySpecifications(request.CommunityId.Value));
        }

        if (request.RequestFilter == eRequestFilter.Owner)
        {
            spec = spec.And(new ServiceRequestOwnerSpecifications(sess.User.Id));
            // q = q.Where(n => n.OwnerId == request.OwnerId.Value);
        }
        else if (request.RequestFilter == eRequestFilter.NewCases)
        {
            spec = spec.And(new ServiceRequestIsInGroupListSpecifications(sess.GroupIds()));
            spec = spec.And(new ServicerequestIsNewForUserSpecifications(sess.User.Id));
        }
        else if (request.RequestFilter == eRequestFilter.NewAnnotations)
        {
            spec = spec.And(new ServiceRequestIsInGroupListSpecifications(sess.GroupIds()));
            spec = spec.And(new ServiceRequestHasNewAnnotationForUserSpecifications(sess.User.Id));
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
        spec = spec.And(new ServiceRequestIsVisibleSpecifications(sess.IsAuthenticated ? sess.User.Id : null));
        q = q.Where(spec.ToExpression());


        // filter & sort
        q = q.ApplyQuery(request);

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