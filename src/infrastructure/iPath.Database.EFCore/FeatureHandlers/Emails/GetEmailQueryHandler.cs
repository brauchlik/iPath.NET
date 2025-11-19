using iPath.EF.Core.Database;

namespace iPath.EF.Core.FeatureHandlers.Emails;

internal class GetEmailQueryHandler(iPathDbContext db)
     : IRequestHandler<GetEmailsQuery, Task<PagedResultList<EmailMessage>>>
{
    public async Task<PagedResultList<EmailMessage>> Handle(GetEmailsQuery request, CancellationToken cancellationToken)
    {
        var q = db.EmailStore.AsNoTracking()
            .OrderByDescending(e => e.CreatedOn);
        var res = await q.ToPagedResultAsync(request, cancellationToken);
        return res;
    }
}
