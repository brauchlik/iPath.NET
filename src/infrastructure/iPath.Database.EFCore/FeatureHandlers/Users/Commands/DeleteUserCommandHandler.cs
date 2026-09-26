using Microsoft.AspNetCore.Identity;

namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class DeleteUserCommandHandler(UserManager<User> um, IMediator mediator, IUserSession sess)
     : IRequestHandler<DeleteUserCommand, Task>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await um.FindByIdAsync(request.UserId.ToString());
        Guard.Against.NotFound(request.UserId, user);

        await um.DeleteAsync(user);
        sess.ReloadUser(user.Id);
    }
}
