using System.ComponentModel.DataAnnotations;

namespace iPath.Application.Contracts;

public interface IEmailSender
{
    Task<Result> SendMailAsync([EmailAddress] string address, string subject, string body, CancellationToken ct = default);
    Task<bool> LoginAsync(CancellationToken ct = default);
}
