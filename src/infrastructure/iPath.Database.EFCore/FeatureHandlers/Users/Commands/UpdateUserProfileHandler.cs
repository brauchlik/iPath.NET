using iPath.Application.Exceptions;

namespace iPath.Application.Features.Users;

public class UpdateUserProfileHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<UpdateUserProfileCommand, Task<Guid>>
{
    public async Task<Guid> Handle(UpdateUserProfileCommand request, CancellationToken ct)
    {
        // validate that session user is admin or equals user to modify
        if (!sess.CanModifyUser(request.UserId))
        {
            throw new NotAllowedException("You are not allowed to update another user");
        }

        // get the User from DB
        var user = await db.Users.FindAsync(request.UserId, ct);
        Guard.Against.NotFound(request.UserId, user);

        await using var trans = await db.Database.BeginTransactionAsync(ct);
        try
        {
            user.UpdateProfile(request.Profile);
            // db.Users.Update(user);

            var evt = EventEntity.Create<UserProfileUpdatedEvent, UpdateUserProfileCommand>(request, user.Id, sess.User.Id);
            await db.EventStore.AddAsync(evt, ct);

            await db.SaveChangesAsync(ct);
            await trans.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await trans.RollbackAsync();
            throw ex;
        }

        // Refresh the cache
        sess.ReloadUser(user.id);

        return user.Id;
    }
}