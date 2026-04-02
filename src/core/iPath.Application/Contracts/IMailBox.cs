namespace iPath.Application.Contracts;

public interface IMailBox : IAsyncDisposable
{
    string MailboxName { get; }
    
    Task ConnectAsync(CancellationToken ct = default);
    
    Task<IReadOnlyList<MailHeader>> ListEmailsAsync(DateTime? since = null, CancellationToken ct = default);
    
    Task<MailMessage?> GetEmailAsync(string messageId, CancellationToken ct = default);
    
    Task MoveToFolderAsync(string messageId, string folderName, CancellationToken ct = default);
    
    Task DeleteEmailAsync(string messageId, CancellationToken ct = default);
    
    Task EnsureFolderExistsAsync(string folderName, CancellationToken ct = default);
}

public record MailHeader(
    string MessageId,
    string Subject,
    string SenderEmail,
    string? SenderName,
    DateTime ReceivedDate,
    bool HasAttachments
);

public record MailMessage(
    string MessageId,
    string Subject,
    string SenderEmail,
    string? SenderName,
    string? HtmlBody,
    string? PlainTextBody,
    DateTime ReceivedDate,
    IReadOnlyList<MailAttachment> Attachments
);

public record MailAttachment(
    string Filename,
    string ContentType,
    long Size,
    Stream Content
);