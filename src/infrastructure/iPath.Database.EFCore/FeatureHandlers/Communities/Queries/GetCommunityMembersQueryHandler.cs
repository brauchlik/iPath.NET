using iPath.Application;

namespace iPath.EF.Core.FeatureHandlers.Communities.Queries;

public class GetCommunityMembersQueryHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetCommunityMembersQuery, Task<PagedResultList<CommunityMemberDto>>>
{
    public async Task<PagedResultList<CommunityMemberDto>> Handle(GetCommunityMembersQuery request, CancellationToken cancellationToken)
    {
        var q = db.Set<CommunityMember>()
            .Include(m => m.User)
            .AsNoTracking()
            .Where(c => c.CommunityId == request.CommunityId);

        q.ApplyQuery(request);
        if (request.Sorting.IsEmpty())
        {
            q = q.OrderBy(m => m.User.UserName);
        }

        var projected = q.Select(m => new CommunityMemberDto(CommunityId: m.CommunityId, UserId: m.UserId, Role: m.Role, Communityname: m.Community.Name, Username: m.User.UserName));
        return await projected.ToPagedResultAsync(request, cancellationToken);
    }
}
