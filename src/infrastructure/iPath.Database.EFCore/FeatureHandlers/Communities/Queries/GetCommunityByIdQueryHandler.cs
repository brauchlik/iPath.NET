namespace iPath.EF.Core.FeatureHandlers.Communities.Queries;

public class GetCommunityByIdQueryHandler(iPathDbContext db)
    : IRequestHandler<GetCommunityByIdQuery, Task<CommunityDto>>
{
    public async Task<CommunityDto> Handle(GetCommunityByIdQuery request, CancellationToken cancellationToken)
    {
        var community = await db.Communities.AsNoTracking()
            .Where(c => c.Id == request.id)
            .Select(c => new CommunityDto(Id: c.Id, Name: c.Name, Description: c.Description, BaseUrl: c.BaseUrl,
                 Groups: c.Groups.Select(g => new GroupListDto(g.Group.Id, g.Group.Name)).ToArray(),
                 Owner: c.Owner.ToOwnerDto()))
            .FirstOrDefaultAsync(cancellationToken);
        Guard.Against.NotFound(request.id, community);
        return community;
    }
}

