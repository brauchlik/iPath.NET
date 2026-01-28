using iPath.Application.Exceptions;

namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class UpdateCommunityMembershipHandler(iPathDbContext db, IMediator mediator, IUserSession sess)
    : IRequestHandler<UpdateCommunityMembershipCommand, Task<UserDto>>
{
    public async Task<UserDto> Handle(UpdateCommunityMembershipCommand request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync(request.UserId, ct);
        Guard.Against.NotFound(request.UserId, user);

        // validate input
        if (request.Membership.Any(m => m.UserId != request.UserId))
        {
            throw new ArgumentException("All Membership entries must have the same UserId as the request");
        }

        var set = db.Set<CommunityMember>();            

        // Validate that User is allowed to modify Communitys
        if (sess.IsAdmin)
        {
            // admin may update all
        }
        else 
        {
            // validate that the current user can moderate the community
            var moderatedCommunityIds = await set.AsNoTracking().Where(m => m.UserId == sess.User.Id && m.Role == eMemberRole.Moderator)
                .Select(m => m.CommunityId).ToHashSetAsync(ct);

            // Moderator mayb update only the Grouops for which they have moderator role
            foreach (var item in request.Membership)
            {
                if (!moderatedCommunityIds.Contains(item.CommunityId))
                    throw new NotAllowedException();
            }
        }


        // reload from DB
        var communityIds = request.Membership.Select(m => m.CommunityId).ToHashSet();
        var list = await set
            .Where(m => m.UserId == request.UserId)
            .Where(m => communityIds.Contains(m.CommunityId))
            .ToListAsync(ct);

        foreach (var dto in request.Membership)
        {
            if (dto.Role == eMemberRole.None)
            {
                // Role None => Remove from List
                var entity = list.FirstOrDefault(m => m.CommunityId == dto.CommunityId);
                if (entity != null)
                {
                    set.Remove(entity);
                }
            }
            else
            {
                // upsert
                var entity = list.FirstOrDefault(m => m.CommunityId == dto.CommunityId);
                if (entity == null)
                {
                    entity = new CommunityMember()
                    {
                        UserId = request.UserId,
                        CommunityId = dto.CommunityId,
                    };
                    await set.AddAsync(entity, ct);
                }
                entity.Role = dto.Role;
                entity.IsConsultant = dto.IsConsultant;
            }
        }

        // User is derived from Identity User and thus does not have domain events => save events directly
        var evt = EventEntity.Create<CommunityMembershipUpdatedEvent, UpdateCommunityMembershipCommand>(request, user.Id, sess.User.Id);
        await db.EventStore.AddAsync(evt, ct);

        await db.SaveChangesAsync(ct);

        return await mediator.Send(new GetUserByIdQuery(user.Id), ct);
    }
}