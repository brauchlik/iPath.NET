namespace iPath.Application.Features.ServiceRequests;

public class GetServiceRequestsQueryBase : PagedQuery<ServiceRequestListDto>
{
    public Guid? GroupId { get; set; }

    // limit query to a community
    public Guid? CommunityId { get; set; }

    public required eRequestFilter RequestFilter { get; set; } = eRequestFilter.Group;
    public string SearchString { get; set; }
}
