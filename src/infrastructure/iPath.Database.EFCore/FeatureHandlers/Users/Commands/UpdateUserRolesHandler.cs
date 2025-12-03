using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class UpdateUserAccountCommandHandler(UserManager<User> um, RoleManager<Role> rm, IUserSession sess, ILogger<UpdateUserRoleHandler> logger)
    : IRequestHandler<UpdateUserAccountCommand, Task>
{
    public async Task Handle(UpdateUserAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await um.FindByIdAsync(request.UserId.ToString());
        Guard.Against.NotFound(request.UserId, user);


        if ( request.IsActive.HasValue)
        {
            user.UpdateActive(request.IsActive.Value);
        }

        if (request.Profile != null)
        {
            user.UpdateProfile(request.Profile);
        }

        if (request.Username != null)
        {
            // not implemented yet
            throw new NotImplementedException();
        }

        await um.UpdateAsync(user);


        // save roles
        if (request.Roles != null)
        {
            foreach (var role in await rm.Roles.ToListAsync(cancellationToken))
            {
                if (request.Roles.Contains(role.Id))
                {
                    // add role
                    await um.AddToRoleAsync(user, role.Name);
                }
                else
                {
                    // remove role
                    await um.RemoveFromRoleAsync(user, role.Name);
                }
            }
        }

        // Refresh the cache
        sess.ReloadUser(user.Id);
    }
}