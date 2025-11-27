
using iPath.Application.Contracts;

namespace iPath.EF.Core.FeatureHandlers.Communities.Commands;

public class UpdateCommunityHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<UpdateCommunityCommand, Task<CommunityListDto>>
{
    public async Task<CommunityListDto> Handle(UpdateCommunityCommand request, CancellationToken ct)
    {
        var community = await db.Communities.FindAsync(request.Id, ct);
        Guard.Against.NotFound(request.Id, community);

        if (request.Name is not null)
        {
            Guard.Against.Empty(request.Name, "Name must not be empty");
            var existing = await db.Communities.AsNoTracking().IgnoreQueryFilters().AnyAsync(x => x.Id != request.Id && x.Name == request.Name, ct);
            if (existing) throw new CommunityNameExistsException(request.Name);
            community.Name = request.Name;
        }

        if (request.OwnerId.HasValue)
        {
            var user = await db.Users.FindAsync(request.OwnerId.Value, ct);
            community.Owner = Guard.Against.NotFound(request.OwnerId.Value.ToString(), user);
        }

        if (request.Description is not null) community.Description = request.Description;
        if (request.BaseUrl is not null) community.BaseUrl = request.BaseUrl;


        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        db.Communities.Update(community);

        // create & save event
        var evt = EventEntity.Create<CommunityUpdatedEvent, UpdateCommunityCommand>(request, objectId: community.Id, userId: sess.User.Id);
        await db.EventStore.AddAsync(evt, ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return community.ToListDto();
    }
}
