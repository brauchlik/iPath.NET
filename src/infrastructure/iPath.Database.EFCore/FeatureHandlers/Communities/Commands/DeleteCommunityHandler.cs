using iPath.Application.Contracts;
using Microsoft.AspNetCore.Identity;

namespace iPath.EF.Core.FeatureHandlers.Communities.Commands;

public class DeleteCommunityHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<DeleteCommunityCommand, Task<Guid>>
{
    public async Task<Guid> Handle(DeleteCommunityCommand request, CancellationToken ct)
    {
        var community = await db.Communities.FindAsync(request.Id, ct);
        Guard.Against.NotFound(request.Id, community);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        db.Communities.Remove(community);

        // create & save event
        var evt = EventEntity.Create<CommunityDeletedEvent, DeleteCommunityCommand>(request, objectId: community.Id, userId: sess.User.Id);
        await db.EventStore.AddAsync(evt, ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return request.Id;
    }
}
