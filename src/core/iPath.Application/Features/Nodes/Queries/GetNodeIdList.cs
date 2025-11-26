namespace iPath.Application.Features.Nodes;



public class GetNodeIdListQuery : PagedQuery<NodeListDto>
    , IRequest<GetNodeIdListQuery, Task<IReadOnlyList<Guid>>>
{
    public GetNodeIdListQuery()
    {   
    }

    public GetNodeIdListQuery(GetNodesQuery q) 
    { 
        GroupId = q.GroupId;
        OwnerId = q.OwnerId;
        Sorting = q.Sorting;
        Filter = q.Filter;
        PageSize = null;
        Page = 0;
    }

    public Guid? GroupId { get; set; }
    public Guid? OwnerId { get; set; }
    public bool inclDrafts { get; set; } = false;
}
