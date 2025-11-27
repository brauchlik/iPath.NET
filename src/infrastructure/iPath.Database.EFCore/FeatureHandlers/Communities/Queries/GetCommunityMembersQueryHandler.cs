using iPath.Application.Features; 

namespace iPath.EF.Core.FeatureHandlers.Communities.Queries;

public class GetCommunityMembersQueryHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetCommunityMembersQuery, Task<IEnumerable<CommunityMemberDto>>>
{
    public async Task<IEnumerable<CommunityMemberDto>> Handle(GetCommunityMembersQuery request, CancellationToken cancellationToken)
    {



        var c = await db.Communities
            .Include(c => c.Members)
            .ThenInclude(m => m.User)
            .AsNoTracking()
            .Where(c => c.Id == request.id)
            .SelectMany(c => c.Members.Select(m => new CommunityMemberDto(CommunityId: c.Id, UserId: m.User.Id, Role: m.Role, Username: m.User.UserName)))
            .ToListAsync(cancellationToken);
        return c;
    }
}
