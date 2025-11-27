
namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class AssignUserToCommunityCommandHandler(iPathDbContext db)
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

        // find existing membership
        var m = user.CommunityMembership.FirstOrDefault(m => m.CommunityId == request.communityId);
        if (request.role == eMemberRole.None)
        {
            // role None => remove
            if (m != null) user.CommunityMembership.Remove(m);
        }
        else
        { 
            if (m is null)
            {
                // new membership
                user.CommunityMembership.Add(new CommunityMember { User = user, Community = community, Role = request.role });
            }
            else
            {
                // change of role
                m.Role = request.role;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
