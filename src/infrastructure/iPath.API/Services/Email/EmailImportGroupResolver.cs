using iPath.Application.Contracts;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.API.Services.Email;

public class EmailImportGroupResolver : IEmailImportGroupResolver
{
    private readonly iPathDbContext _db;

    public EmailImportGroupResolver(iPathDbContext db)
    {
        _db = db;
    }

    public async Task<(Guid GroupId, Guid? UserId)?> ResolveGroupAsync(
        string mailboxName,
        string senderEmail,
        CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Email == senderEmail, ct);

        if (user == null)
            return null;

        if (user.Profile?.EmailImportSettings?.DefaultGroupId != null)
        {
            return (user.Profile.EmailImportSettings.DefaultGroupId.Value, user.Id);
        }

        return null;
    }
}