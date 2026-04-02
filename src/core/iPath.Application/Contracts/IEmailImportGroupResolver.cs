using iPath.Application.Features.EmailImport;

namespace iPath.Application.Contracts;

public interface IEmailImportGroupResolver
{    Task<Result<EmailImportGroupResolverResult>> ResolveGroupAsync(
        ImapConfig? mailboxConfig,
        string senderEmail,
        CancellationToken ct);
}

public record EmailImportGroupResolverResult(Guid GroupId, Guid UserId);