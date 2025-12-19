using FluentResults;
using Microsoft.AspNetCore.Identity;

namespace iPath.Application.Features.Users;

public class UpdateUserPasswordHandler(UserManager<User> um, IUserSession sess)
    : IRequestHandler<UpdateUserPasswordCommand, Task<Result>>
{
    public async Task<Result> Handle(UpdateUserPasswordCommand request, CancellationToken ct)
    {
        // validate that session user is admin 
        if (!sess.IsAdmin)
        {
            throw new NotAllowedException("You are not allowed to update another user");
        }

        // get the User from DB
        var user = await um.FindByIdAsync(request.UserId.ToString());
        Guard.Against.NotFound(request.UserId, user);

        // set password
        var tk = await um.GeneratePasswordResetTokenAsync(user);
        var res = await um.ResetPasswordAsync(user, tk, request.Password);

        // set email validated
        if (res.Succeeded && request.SetEmailValidated)
        {
            var etk = await um.GenerateEmailConfirmationTokenAsync(user);
            res = await um.ConfirmEmailAsync(user, etk);
        }

        if (res.Succeeded)
        {
            return Result.Ok();
        }
        else
        {
            return Result.Fail(res.Errors.FirstOrDefault().Description);
        }
    }
}