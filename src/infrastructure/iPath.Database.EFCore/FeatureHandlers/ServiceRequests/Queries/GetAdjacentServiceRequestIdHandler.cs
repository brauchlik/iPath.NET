using iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests;

public class GetAdjacentServiceRequestIdHandler(
    iPathDbContext db,
    IUserSession sess,
    ILogger<GetAdjacentServiceRequestIdHandler> logger)
    : IRequestHandler<GetAdjacentServiceRequestIdQuery, Task<Guid?>>
{
    public async Task<Guid?> Handle(GetAdjacentServiceRequestIdQuery request, CancellationToken cancellationToken)
    {
        var current = await db.ServiceRequests.AsNoTracking()
            .FirstOrDefaultAsync(sr => sr.Id == request.CurrentId, cancellationToken);
        if (current is null) return null;

        var q = db.ServiceRequests.AsNoTracking();
        q = q.ApplyRequest(request.Query, sess);

        if (request.Direction > 0)
        {
            q = q.Where(sr => sr.CreatedOn < current.CreatedOn);
            q = q.OrderByDescending(sr => sr.CreatedOn).ThenBy(sr => sr.Id);
        }
        else
        {
            q = q.Where(sr => sr.CreatedOn > current.CreatedOn);
            q = q.OrderBy(sr => sr.CreatedOn).ThenByDescending(sr => sr.Id);
        }

        return await q.Select(sr => (Guid?)sr.Id).FirstOrDefaultAsync(cancellationToken);
    }
}