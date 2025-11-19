using iPath.Application.Contracts;
using iPath.Application.Features;
using Microsoft.AspNetCore.Identity;


namespace iPath.Blazor.Server.Components.Account
{
    // Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
    internal sealed class IdentitySmtpSender(iPath.Application.Contracts.IEmailSender sender) : IEmailSender<User>
    {
        public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink) =>
            sender.SendMailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

        public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
        {
            if (resetLink.Contains("email="))
            {
                // migration confirmation
                return sender.SendMailAsync(email, "iPath Account Migration", 
                    $"Please confirm your account migration by <a href='{resetLink}'>clicking here</a>.");
            }
            else
            {
                return sender.SendMailAsync(email, "Reset your iPath password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");
            }
        }
            
        public Task SendPasswordResetCodeAsync(User user, string email, string resetCode) =>
            sender.SendMailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");
    }
}
