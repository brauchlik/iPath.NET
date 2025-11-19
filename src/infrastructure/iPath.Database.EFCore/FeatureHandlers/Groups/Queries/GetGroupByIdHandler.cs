using iPath.Application.Contracts;

namespace iPath.EF.Core.FeatureHandlers.Groups.Queries;

public class GetGroupByIdHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<GetGroupByIdQuery, Task<GroupDto>>
{
    public async Task<GroupDto> Handle(GetGroupByIdQuery request, CancellationToken cancellationToken)
    {
        sess.AssertInGroup(request.GroupId);

        var community = await db.Groups.AsNoTracking()
            .Where(g => g.Id == request.GroupId)
            .Select(g => new GroupDto(Id: g.Id, Name: g.Name, Settings: g.Settings,
                                      Members: g.Members.Select(m => new GroupMemberDto(UserId: m.User.Id, Username: m.User.UserName, Role: m.Role)).ToArray()))
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.GroupId, community);

        return community;
    }
}
