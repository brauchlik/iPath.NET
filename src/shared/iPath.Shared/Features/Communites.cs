using DispatchR.Abstractions.Send;

namespace iPath.Shared.Features;

public record CommunityListDto(Guid Id, string Name);

public class CommunityListQuery : PagedQuery<CommunityListDto>
    , IRequest<CommunityListQuery, Task<PagedResult<CommunityListDto>>>
{
}
