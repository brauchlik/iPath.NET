namespace iPath.Application.Features.ServiceRequests;



public class GetServiceRequestsQuery : PagedQuery<ServiceRequestListDto>
    , IRequest<GetServiceRequestsQuery, Task<PagedResultList<ServiceRequestListDto>>>
{
    public Guid? GroupId { get; set; }

    // limit query to a community
    public Guid? CommunityId { get; set; }

    public required eRequestFilter RequestFilter { get; set; } = eRequestFilter.Group;

    public bool IncludeDetails { get; set; }
    public string SearchString { get; set; }
}



public enum eRequestFilter
{
    Group = 0,            // Requests in Group
    Owner = 1,             // My Cases
    NewCases = 3,         // New Cases (unvisited)
    NewAnnotations = 4    // Cases with new annotations (univsited)
}