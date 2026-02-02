namespace iPath.Application.Features.ServiceRequests;

public class GetServiceRequestListQuery : GetServiceRequestsQueryBase
    , IRequest<GetServiceRequestListQuery, Task<PagedResultList<ServiceRequestListDto>>>
{
    public bool IncludeDetails { get; set; }
}



public enum eRequestFilter
{
    None = 0,
    Group = 1,            // Requests in Group
    Owner = 2,            // My Cases
    Search = 3,           // Search => all cases
    NewCases =43,         // New Cases (unvisited)
    NewAnnotations = 5    // Cases with new annotations (univsited)
}