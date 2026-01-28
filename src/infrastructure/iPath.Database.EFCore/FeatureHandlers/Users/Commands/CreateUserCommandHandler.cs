using Microsoft.AspNetCore.Identity;

namespace iPath.EF.Core.FeatureHandlers.Users.Commands;

public class CreateUserCommandHandler(UserManager<User> um, IMediator mediator)
     : IRequestHandler<CreateUserCommand, Task<OwnerDto>>
{
    public async Task<OwnerDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var newUser = new User
        {
            UserName = request.Username,
            Email = request.Email,
        };
        var res = await um.CreateAsync(newUser);
        if (!res.Succeeded)
        {
            throw new Exception(res.Errors.FirstOrDefault().Description);
        }

        if (request.Community != null)
        {
            var cmd = new AssignUserToCommunityCommand(userId: newUser.Id, communityId: request.Community.Id, role: eMemberRole.User, isConsultant: false);
            await mediator.Send(cmd, cancellationToken);
        }

        return newUser.ToOwnerDto();
    }
}
