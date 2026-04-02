namespace iPath.Application.Contracts;

public interface IEmailImportGroupResolver
{
    Task<(Guid GroupId, Guid? UserId)?> ResolveGroupAsync(
        string mailboxName,
        string senderEmail,
        CancellationToken ct);
}