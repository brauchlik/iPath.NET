
namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class AssignUserToGroupsCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<AssignUserToGroupCommand, Task<GroupMemberDto>>
{
    public async Task<GroupMemberDto> Handle(AssignUserToGroupCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.GroupMembership)
            .FirstOrDefaultAsync(u => u.Id == request.userId);
        Guard.Against.NotFound(request.userId, user);

        var group = await db.Groups.FindAsync(request.groupId);
        Guard.Against.NotFound(request.groupId, group);

        // this function only assigns a normal user role, if user not in group yet.
        var m = user.GroupMembership.FirstOrDefault(m => m.GroupId == request.groupId);
        if (m is null)
        {
            // new membership
            m = new GroupMember { User = user, Group = group, Role = request.role };
            user.GroupMembership.Add(m);
        }
        else
        {
            m.Role = request.role;
        }
        await db.SaveChangesAsync(cancellationToken);

        // Refresh the cache
        sess.ReloadUser(request.userId);

        return new GroupMemberDto(m.UserId, user.UserName, m.Role);
    }
}
