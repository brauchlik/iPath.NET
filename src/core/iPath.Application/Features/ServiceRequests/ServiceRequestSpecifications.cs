using System.Linq.Expressions;
using System.Threading.Tasks.Dataflow;

namespace iPath.Application.Features.ServiceRequests;


public class ServiceRequestIsVisibleSpecifications(Guid? userId) : Specification<ServiceRequest>
{
    public override Expression<Func<ServiceRequest, bool>> ToExpression()
    {
        // node is either owned by the current user or it is not draft and also visible
        return (n => (userId.HasValue && n.OwnerId == userId) || (n.Visibility != eNodeVisibility.Private && !n.IsDraft));
    }
}

public class ServiceRequestIsPublicSpecifications : Specification<ServiceRequest>
{
    public override Expression<Func<ServiceRequest, bool>> ToExpression()
    {
        // node is either owned by the current user or it is not draft and also visible
        return (n => n.Visibility == eNodeVisibility.Private);
    }
}

public class ServiceRequestOwnerSpecifications(Guid ownerId) : Specification<ServiceRequest>
{
    public override Expression<Func<ServiceRequest, bool>> ToExpression()
    {
        return (n => n.OwnerId == ownerId);
    }
}

public class ServiceRequestIsInGroupSpecifications(Guid groupId) : Specification<ServiceRequest>
{
    public override Expression<Func<ServiceRequest, bool>> ToExpression()
    {
        return (n => n.GroupId == groupId);
    }
}

public class ServiceRequestIsInGroupListSpecifications(HashSet<Guid> groupIds) : Specification<ServiceRequest>
{
    public override Expression<Func<ServiceRequest, bool>> ToExpression()
    {
        return (n => groupIds.Contains(n.GroupId));
    }
}

public class ServiceRequestIsInCommunitySpecifications(Guid communityId) : Specification<ServiceRequest>
{
    public override Expression<Func<ServiceRequest, bool>> ToExpression()
    {
        // node is either owned by the current user or it is not draft and also visible
        return (n => n.Group.CommunityId == communityId);
    }
}



public class ServicerequestIsNewForUserSpecifications(Guid userId, DateTime? minDate = null) : Specification<ServiceRequest>
{
    public override Expression<Func<ServiceRequest, bool>> ToExpression()
    {
        minDate ??= DateTime.UtcNow.AddYears(-1);

        // node is either owned by the current user or it is not draft and also visible
        return (n => !n.IsDraft && n.CreatedOn > minDate && !n.LastVisits.Any(v => v.UserId == userId));
    }
}


public class ServiceRequestHasNewAnnotationForUserSpecifications(Guid userId, DateTime? minDate = null) : Specification<ServiceRequest>
{
    public override Expression<Func<ServiceRequest, bool>> ToExpression()
    {
        minDate ??= DateTime.UtcNow.AddYears(-1);

        // node is either owned by the current user or it is not draft and also visible
        return (n => !n.IsDraft && n.Annotations.Any(a => a.CreatedOn > minDate && a.OwnerId != userId &&
                 (!n.LastVisits.Any(v => v.UserId == userId) || a.CreatedOn > n.LastVisits.First(v => v.UserId == userId).Date)));
    }
}