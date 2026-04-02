using iPath.EF.Core.FeatureHandlers.ServiceRequests.Queries;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests;


public class GetServiceRequestIdListHandler(iPathDbContext db, IUserSession sess, ILogger<GetServiceRequestIdListHandler> logger)
    : IRequestHandler<GetServiceRequestIdListQuery, Task<IReadOnlyList<Guid>>>
{
    public async Task<IReadOnlyList<Guid>> Handle(GetServiceRequestIdListQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(sess.User);

        logger.LogInformation("Loading ServiceRequest IDs for user {UserId}", sess.User.Id);

        // prepare query (only root nodes)
        var q = db.ServiceRequests.AsNoTracking();
        q = q.ApplyRequest(request, sess);

        var result = await q.Select(sr => sr.Id).ToListAsync(cancellationToken);

        logger.LogInformation("Loaded {Count} ServiceRequest IDs", result.Count);

        return result;
    }
}
