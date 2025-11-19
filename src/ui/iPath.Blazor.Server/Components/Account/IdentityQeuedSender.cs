using DispatchR;
using iPath.Application.Contracts;
using iPath.Application.Features;
using Microsoft.AspNetCore.Identity;


namespace iPath.Blazor.Server.Components.Account
{
    internal sealed class IdentityQueuedSender(IMediator mediator) : IEmailSender<User>
    {
       
        private async Task Enqueue(CreateEmailCommand mail)
            => await mediator.Send(mail, default);

        public async Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
            => Enqueue(new CreateEmailCommand(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>."));
           

        public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
        {
            if (resetLink.Contains("email="))
            {
                // migration confirmation
                return Enqueue(new CreateEmailCommand(email, "iPath Account Migration", 
                    $"Please confirm your account migration by <a href='{resetLink}'>clicking here</a>."));
            }
            else
            {
                return Enqueue(new CreateEmailCommand(email, "Reset your iPath password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>."));
            }
        }
            
        public Task SendPasswordResetCodeAsync(User user, string email, string resetCode) =>
            Enqueue(new CreateEmailCommand(email, "Reset your password", $"Please reset your password using the following code: {resetCode}"));
    }
}
