
namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class AssignUserToGroupsCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<AssignUserToGroupCommand, Task>
{
    public async Task Handle(AssignUserToGroupCommand request, CancellationToken cancellationToken)
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
            user.GroupMembership.Add(new GroupMember { User = user, Group = group, Role = eMemberRole.User });
            await db.SaveChangesAsync(cancellationToken);
        }

        // Refresh the cache
        sess.ReloadUser(request.userId);
    }
}
