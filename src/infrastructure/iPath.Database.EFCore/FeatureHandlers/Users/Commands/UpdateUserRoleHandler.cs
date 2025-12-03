using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class UpdateUserRoleHandler(UserManager<User> um, RoleManager<Role> rm, IUserSession sess, ILogger<UpdateUserRoleHandler> logger)
    : IRequestHandler<UpdateUserRoleCommand, Task<Guid>>
{
    public async Task<Guid> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await um.FindByIdAsync(request.UserId.ToString());
        Guard.Against.NotFound(request.UserId, user);

        var role = await rm.FindByIdAsync(request.RoleId.ToString());
        Guard.Against.NotFound(request.RoleId, role);

        IdentityResult result;
        if (request.allow)
        {
            result = await um.AddToRoleAsync(user, role.Name);
        }
        else
        {
            result = await um.RemoveFromRoleAsync(user, role.Name);
        }

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogWarning($"Failed to {(request.allow ? "add" : "remove")} role '{role.Name}' for user '{user.Id}': {errors}");
        }

        // Refresh the cache
        sess.ReloadUser(user.Id);

        return user.Id;
    }
}