namespace iPath.Application.Features.Nodes;



public class GetNodeIdListQuery : PagedQuery<NodeListDto>
    , IRequest<GetNodesQuery, Task<IReadOnlyList<Guid>>>
{
    public Guid? GroupId { get; set; }
    public Guid? OwnerId { get; set; }
}
