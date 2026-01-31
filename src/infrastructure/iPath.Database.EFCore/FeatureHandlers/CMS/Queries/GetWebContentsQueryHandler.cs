using iPath.Application.Features.CMS;

namespace iPath.EF.Core.FeatureHandlers.CMS.Queries;

public class GetWebContentsQueryHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetWebContentsQuery, Task<PagedResultList<WebContentDto>>>
{
    public async Task<PagedResultList<WebContentDto>> Handle(GetWebContentsQuery request, CancellationToken cancellationToken)
    {
        var q = db.WebPages.AsNoTracking();

        // filter & sort
        q = q.ApplyQuery(request, "CreatedOn DESC");

        // project
        var projeted = q.Select(x => new WebContentDto(Id: x.Id, Title: x.Title, Body: x.Body, Type: x.Type, CreatedOn: x.CreatedOn,
            Owner: new OwnerDto(Id: x.Owner.Id, Username: x.Owner.UserName, Email: x.Owner.Email)));

        // pagination
        return await projeted.ToPagedResultAsync(request, cancellationToken);
    }
}
