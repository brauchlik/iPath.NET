
namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class AssignUserToCommunityCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<AssignUserToCommunityCommand, Task>
{
    public async Task Handle(AssignUserToCommunityCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.CommunityMembership)
            .FirstOrDefaultAsync(u => u.Id == request.userId);
        Guard.Against.NotFound(request.userId, user);

        var community = await db.Communities.FindAsync(request.communityId);
        Guard.Against.NotFound(request.communityId, community);

        if (request.role == eMemberRole.None)
        {
            user.RemoveFromCommunity(community);
        }
        else
        {
            user.AddToCommunity(community, request.role);
        }

        // Refresh the cache
        sess.ReloadUser(request.userId);

        await db.SaveChangesAsync(cancellationToken);
    }
}
