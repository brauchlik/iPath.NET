

namespace iPath.EF.Core.FeatureHandlers.Groups.Commands;

public class AssignGroupToCommunityHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<AssignGroupToCommunityCommand, Task<GroupAssignedToCommunityEvent>>
{
    public async Task<GroupAssignedToCommunityEvent> Handle(AssignGroupToCommunityCommand request, CancellationToken ct)
    {
        Guard.Against.Null(sess.User);
        sess.AssertInRole("Admin");

        var group = await db.Groups
            .Include(x => x.Communities)
            .FirstOrDefaultAsync(x => x.Id == request.GroupId, ct);
        Guard.Against.NotFound(request.GroupId.ToString(), group);

        var community = await db.Communities.FindAsync(new object[] { request.CommunityId }, ct);
        Guard.Against.NotFound(request.CommunityId.ToString(), community);


        // prepare changes
        if (request.Remove)
        {
            var toRemove = group.Communities.FirstOrDefault(x => x.CommunityId == request.CommunityId);
            if (toRemove != null)
            {
                group.Communities.Remove(toRemove);
            }
        }
        else
        {
            var exists = group.Communities.Any(x => x.CommunityId == request.CommunityId);
            if (!exists)
            {
                group.Communities.Add(new CommunityGroup()
                {
                    Group = group,
                    Community = community
                });
            }
        }

        await using var trans = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var evt = EventEntity.Create<GroupAssignedToCommunityEvent, AssignGroupToCommunityCommand>(request, objectId: group.Id, userId: sess.User.Id);
            await db.EventStore.AddAsync(evt, ct);

            await db.SaveChangesAsync(ct);
            await trans.CommitAsync(ct);
            return evt;
        }
        catch (Exception ex)
        {
            await trans.RollbackAsync();
        }
        return null;
    }
}