
using iPath.Application.Contracts;

namespace iPath.EF.Core.FeatureHandlers.Communities.Commands;

public class CreateCommunityHandler(iPathDbContext db, IUserSession sess)
     : IRequestHandler<CreateCommunityCommand, Task<CommunityListDto>>
{
    public async Task<CommunityListDto> Handle(CreateCommunityCommand request, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(request.Name, "A name must be specified");

        var user = await db.Users.FindAsync(request.OwnerId, ct);
        Guard.Against.NotFound(request.OwnerId, user);

        var existing = await db.Communities.AsNoTracking().IgnoreQueryFilters().AnyAsync(x => x.Name == request.Name, ct);
        if (existing) throw new CommunityNameExistsException(request.Name);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var newEntity = new Community()
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            OwnerId = user.Id,
            Settings = new CommunitySettings
            {
                Description = request.Description,
                BaseUrl = request.BaseUrl
            }
        };

        await db.Communities.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        // create & save event
        var evt = EventEntity.Create<CommunityCreatedEvent, CreateCommunityCommand>(request, objectId: newEntity.Id, userId: sess.User.Id);
        await db.EventStore.AddAsync(evt, ct);
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return newEntity.ToListDto();
    }
}
