namespace iPath.EF.Core.FeatureHandlers.Communities.Queries;

public class GetCommunityListQueryHandler (iPathDbContext db)
    : IRequestHandler<GetCommunityListQuery, Task<PagedResultList<CommunityListDto>>>
{
    public async Task<PagedResultList<CommunityListDto>> Handle(GetCommunityListQuery request, CancellationToken cancellationToken)
    {
        var q = db.Communities.AsNoTracking();

        // filter & sort
        q = q.ApplyQuery(request);

        // project
        var projeted = q.Select(x => new CommunityListDto(x.Id, x.Name));

        // pagination
        return await projeted.ToPagedResultAsync(request, cancellationToken);
    }
}
