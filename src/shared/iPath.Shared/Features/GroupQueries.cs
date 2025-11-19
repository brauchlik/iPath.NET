using DispatchR.Abstractions.Send;

namespace iPath.Shared.Features;

public record GroupListDto(Guid Id, string Name, int? TotalNodes = null, int? NewNodes = null, int? NewAnnotation = null);

public class GroupListQuery : PagedQuery<GroupListDto>
    , IRequest<GroupListQuery, Task<PagedResult<GroupListDto>>>
{
    public bool InclideCounts { get; set; }
}
