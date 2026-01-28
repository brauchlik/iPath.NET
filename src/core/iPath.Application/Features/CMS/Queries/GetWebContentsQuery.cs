namespace iPath.Application.Features.CMS;

public class GetWebContentsQuery
    : PagedQuery<WebContentDto>
    , IRequest<GetWebContentsQuery, Task<PagedResultList<WebContentDto>>>
{
    public eWebContentType Type { get; set; }
}
