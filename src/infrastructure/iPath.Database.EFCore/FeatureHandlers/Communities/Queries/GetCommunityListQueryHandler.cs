namespace iPath.EF.Core.FeatureHandlers.Communities.Queries;

public class GetCommunityListQueryHandler (iPathDbContext db)
    : IRequestHandler<GetCommunityListQuery, Task<PagedResultList<CommunityListDto>>>
{
    public async Task<PagedResultList<CommunityListDto>> Handle(GetCommunityListQuery request, CancellationToken cancellationToken)
    {
        var q = db.Communities.AsNoTracking();

        // filter & sort
        q = q.ApplyQuery(request, "Name ASC");

        // project
        var projeted = q.Select(x => new CommunityListDto(x.Id, x.Name, Owner: new OwnerDto(Id: x.Owner.Id, Username: x.Owner.UserName, Email: x.Owner.Email)));

        // pagination
        return await projeted.ToPagedResultAsync(request, cancellationToken);
    }
}
