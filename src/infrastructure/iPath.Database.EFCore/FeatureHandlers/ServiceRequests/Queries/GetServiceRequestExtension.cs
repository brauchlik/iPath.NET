using iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests;

public static class GetServiceRequestExtension
{
    public static IQueryable<ServiceRequest> ApplyRequest(this IQueryable<ServiceRequest> q, GetServiceRequestsQueryBase request, IUserSession sess)
    {

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

        return q;
    }
}