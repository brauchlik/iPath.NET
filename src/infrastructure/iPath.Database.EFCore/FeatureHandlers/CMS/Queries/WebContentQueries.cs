using iPath.Application.Features.CMS;

namespace iPath.EF.Core.FeatureHandlers.CMS.Queries;

public class GetWebContentsQuery : PagedQuery<WebContentDto>
{
    public eWebContentType Type { get; set; }
}
