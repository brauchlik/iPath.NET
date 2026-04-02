using iPath.Application.Contracts;
using iPath.Application.Features.EmailImport;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.API.Services.Email;

public class EmailImportGroupResolver(iPathDbContext db) : IEmailImportGroupResolver
{
    public async Task<Result<EmailImportGroupResolverResult>> ResolveGroupAsync(
        ImapConfig? mailboxConfig,
        string senderEmail,
        CancellationToken ct)
    {
        // resolve User
        var normalizedEmail = senderEmail.ToLowerInvariant();
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail || u.Email == normalizedEmail, ct);

        if (user is null)
        {
            return Result.Fail("User not found");
        }

        // First check mailbox config's DefaultGroupId
        Guid? groupId = mailboxConfig?.DefaultGroupId;

        // Fallback: check user's profile DefaultGroupId
        if (!groupId.HasValue)
        {
            groupId = user.Profile.EmailImportSettings?.DefaultGroupId;
        }

        if (!groupId.HasValue)
        {
            return Result.Fail($"Group for User {user.Email} could not be resolved");
        }

        return Result.Ok(new EmailImportGroupResolverResult(groupId.Value, user.Id));
    }
}