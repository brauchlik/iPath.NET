
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


        if (request.role == eMemberRole.None)
        {
            user.RemoveFromGroup(group);
        }
        else
        {
            user.AddToGroup(group, request.role, request.isConsultant);
        }
        await db.SaveChangesAsync(cancellationToken);

        // Refresh the cache
        sess.ReloadUser(request.userId);

        return new GroupMemberDto(user.Id, user.UserName, request.role, request.role != eMemberRole.None && request.isConsultant);
    }
}
