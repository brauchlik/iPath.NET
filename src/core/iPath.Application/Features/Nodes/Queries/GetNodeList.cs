namespace iPath.Application.Features.Nodes;



public class GetNodesQuery : PagedQuery<NodeListDto>
    , IRequest<GetNodesQuery, Task<PagedResultList<NodeListDto>>>
{
    public Guid? GroupId { get; set; }
    public Guid? OwnerId { get; set; }

    public bool IncludeDetails { get; set; }
    public string SearchString { get; set; }
}
