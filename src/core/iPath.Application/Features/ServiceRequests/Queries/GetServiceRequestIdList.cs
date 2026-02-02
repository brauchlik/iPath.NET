namespace iPath.Application.Features.ServiceRequests;



public class GetServiceRequestIdListQuery : GetServiceRequestsQueryBase
    , IRequest<GetServiceRequestIdListQuery, Task<IReadOnlyList<Guid>>>
{
    public static GetServiceRequestIdListQuery From(GetServiceRequestsQueryBase q) => new GetServiceRequestIdListQuery
    {
        RequestFilter = q.RequestFilter,
        CommunityId = q.CommunityId,
        GroupId = q.GroupId,
        Sorting = q.Sorting,
        Filter = q.Filter,
        SearchString = q.SearchString,
        PageSize = null,
        Page = 0
    };
}
