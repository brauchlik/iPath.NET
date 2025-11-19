using FluentResults;
using iPath.Application.Contracts;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;


namespace iPath.Google.Services.Email;

public class SmtpEmailSender(IOptions<SmtpConfig> opts, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<Result> SendMailAsync(SendMailInput mail, CancellationToken ct)
    {
        try
        {
            var cfg = opts.Value;

            var email = new MimeMessage();

            email.From.Add(new MailboxAddress(cfg.SenderName, cfg.SenderEmail));
            email.To.Add(new MailboxAddress("", mail.email));

            email.Subject = mail.subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = mail.body
            };

            using (var smtp = new SmtpClient())
            {
                var tls = opts.Value.SmtpTls
                    ? MailKit.Security.SecureSocketOptions.StartTls
                    : MailKit.Security.SecureSocketOptions.Auto;

                await smtp.ConnectAsync(cfg.SmtpServer, cfg.SmtpPort, tls, ct);

                // Note: only needed if the SMTP server requires authentication
                await smtp.AuthenticateAsync(cfg.SmtpUsername, cfg.SmtpPassword, ct);

                await smtp.SendAsync(email, ct);
                await smtp.DisconnectAsync(true, ct);
            }

            logger.LogInformation("E-Mail sent to " + mail.email);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError("Error sending email to " + mail.email, ex);
            return Result.Fail(ex.Message);
        }
    }


    public async Task<bool> LoginAsync(CancellationToken ct = default)
    {
        try
        {
            var cfg = opts.Value;

            using (var smtp = new SmtpClient())
            {
                var tls = opts.Value.SmtpTls
                    ? MailKit.Security.SecureSocketOptions.StartTls
                    : MailKit.Security.SecureSocketOptions.Auto;

                await smtp.ConnectAsync(cfg.SmtpServer, cfg.SmtpPort, tls, ct);

                // Note: only needed if the SMTP server requires authentication
                await smtp.AuthenticateAsync(cfg.SmtpUsername, cfg.SmtpPassword, ct);
                await smtp.DisconnectAsync(true, ct);
                return true;
            }
        }
        catch (Exception ex)
        {
            return false;
        }
    }
}