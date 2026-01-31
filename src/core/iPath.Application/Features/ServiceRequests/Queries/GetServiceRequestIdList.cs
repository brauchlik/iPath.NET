namespace iPath.Application.Features.ServiceRequests;



public class GetServiceRequestIdListQuery : PagedQuery<ServiceRequestListDto>
    , IRequest<GetServiceRequestIdListQuery, Task<IReadOnlyList<Guid>>>
{
    public GetServiceRequestIdListQuery()
    {   
    }

    public GetServiceRequestIdListQuery(GetServiceRequestsQuery q) 
    {
        RequestFilter = q.RequestFilter;
        CommunityId = q.CommunityId;
        GroupId = q.GroupId;
        Sorting = q.Sorting;
        Filter = q.Filter;
        PageSize = null;
        Page = 0;
    }

    public eRequestFilter RequestFilter { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? CommunityId { get; set; }
    public bool inclDrafts { get; set; } = false;
}
