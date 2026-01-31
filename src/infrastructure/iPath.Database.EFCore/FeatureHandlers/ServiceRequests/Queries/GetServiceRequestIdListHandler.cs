using iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests;


public class GetServiceRequestIdListHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetServiceRequestIdListQuery, Task<IReadOnlyList<Guid>>>
{
    public async Task<IReadOnlyList<Guid>> Handle(GetServiceRequestIdListQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(sess.User);

        // prepare query (only root nodes)
        var q = db.ServiceRequests.AsNoTracking();

        var spec = Specification<ServiceRequest>.All;

        if (request.GroupId.HasValue)
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
        var projeted = q.Select(n => n.Id);

        // paginate
        return await projeted.ToListAsync(cancellationToken);
    }
}