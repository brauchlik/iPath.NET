namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class UpdateGroupMembershipHandler(iPathDbContext db, IMediator mediator, IUserSession sess)
    : IRequestHandler<UpdateGroupMembershipCommand, Task<UserDto>>
{
    public async Task<UserDto> Handle(UpdateGroupMembershipCommand request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync(request.UserId, ct);
        Guard.Against.NotFound(request.UserId, user);

        // Validate that User is allowed to modify groups
        if (sess.IsAdmin)
        {
            // admin may update all
        }
        else if (sess.IsModerator) 
        {
            // Moderator mayb update only the Grouops for which they have moderator role
            foreach (var item in request.Membership)
            {
                if (!sess.IsGroupModerator(item.GroupId)) throw new NotAllowedException();
            }
        }
        else
        {
            throw new NotAllowedException();
        }


        // reload from DB
        var memberSet = db.Set<GroupMember>();
        var groupIds = request.Membership.Select(m => m.GroupId).ToHashSet();
        var list = await memberSet
            .Where(m => m.UserId == request.UserId)
            .Where(m => groupIds.Contains(m.GroupId))
            .ToListAsync(ct);

        // remove those set to None
        foreach (var item in request.Membership.Where(m => m.Role == eMemberRole.None)){
            var dbItem = list.FirstOrDefault(m => m.GroupId == item.GroupId);
            if (dbItem != null)
            {
                memberSet.Remove(dbItem);
            }
        }

        // update and add new
        foreach (var dto in request.Membership)
        {
            var entity = list.FirstOrDefault(m => m.GroupId == dto.GroupId);
            if (entity == null)
            {
                entity = new GroupMember()
                {
                    UserId = request.UserId,
                    GroupId = dto.GroupId,
                };
                await memberSet.AddAsync(entity, ct);
            }
            entity.Role = dto.Role;
        }

        // User is derived from Identity User and thus does not have domain events => save events directly
        var evt = EventEntity.Create<GroupMembershipUpdatedEvent, UpdateGroupMembershipCommand>(request, user.Id, sess.User.Id);
        await db.EventStore.AddAsync(evt, ct);

        await db.SaveChangesAsync(ct);

        // Refresh the cache
        sess.ReloadUser(request.UserId);

        return await mediator.Send(new GetUserByIdQuery(user.Id), ct);
    }
}