namespace iPath.Application.Features.ServiceRequests;

public class GetServiceRequestListQuery : GetServiceRequestsQueryBase
    , IRequest<GetServiceRequestListQuery, Task<PagedResultList<ServiceRequestListDto>>>
{
    public bool IncludeDetails { get; set; }
}



public enum eRequestFilter
{
    Group = 0,            // Requests in Group
    Owner = 1,            // My Cases
    Search = 2,           // Search => all cases
    NewCases = 3,         // New Cases (unvisited)
    NewAnnotations = 4    // Cases with new annotations (univsited)
}