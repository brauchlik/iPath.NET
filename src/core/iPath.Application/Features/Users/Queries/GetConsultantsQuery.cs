using iPath.Application.Querying;

namespace iPath.Application.Features.Users;

public class GetConsultantsQuery : PagedQuery<ConsultantDto>
    , IRequest<GetConsultantsQuery, Task<PagedResultList<ConsultantDto>>>
{
    public Guid? GroupId { get; set; }
    public Guid? CommunityId { get; set; }
    public string? SearchString { get; set; }
    public string? BodySiteCode { get; set; }
}
