using iPath.Application.Contracts;
using iPath.Application.Features.Documents;
using iPath.Application.Features.EmailImport;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Email;

public class EmailImportService : IEmailImportService
{
    private readonly iPathDbContext _db;
    private readonly IMailBoxFactory _mailBoxFactory;
    private readonly IEmailImportGroupResolver _groupResolver;
    private readonly IEmailBodyTextSanitizer _bodySanitizer;
    private readonly IEmailAttachmentNameSanitizer _filenameSanitizer;
    private readonly IMediator _mediator;
    private readonly EmailImportConfig _config;
    private readonly ILogger<EmailImportService> _logger;

    public EmailImportService(
        iPathDbContext db,
        IMailBoxFactory mailBoxFactory,
        IEmailImportGroupResolver groupResolver,
        IEmailBodyTextSanitizer bodySanitizer,
        IEmailAttachmentNameSanitizer filenameSanitizer,
        IMediator mediator,
        IOptions<EmailImportConfig> config,
        ILogger<EmailImportService> logger)
    {
        _db = db;
        _mailBoxFactory = mailBoxFactory;
        _groupResolver = groupResolver;
        _bodySanitizer = bodySanitizer;
        _filenameSanitizer = filenameSanitizer;
        _mediator = mediator;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ImportMailboxSummary>> GetMailboxesAsync(CancellationToken ct)
    {
        var summaries = new List<ImportMailboxSummary>();

        foreach (var mailboxConfig in _config.Mailboxes)
        {
            try
            {
                var since = DateTime.Now.AddDays(-mailboxConfig.DaysToLookBack);

                await using var mailbox = _mailBoxFactory.Create(mailboxConfig);
                await mailbox.ConnectAsync(ct);
                var headers = await mailbox.ListEmailsAsync(since, ct);

                var lastLog = await _db.Set<EmailImportLog>()
                    .Where(l => l.MailboxName == mailboxConfig.Name)
                    .OrderByDescending(l => l.ProcessedOn)
                    .FirstOrDefaultAsync(ct);

                summaries.Add(new ImportMailboxSummary(
                    mailboxConfig.Name,
                    headers.Count,
                    lastLog?.ProcessedOn
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to mailbox {MailboxName}", mailboxConfig.Name);
                summaries.Add(new ImportMailboxSummary(mailboxConfig.Name, 0, null));
            }
        }

        return summaries;
    }

    public async Task<IReadOnlyList<ImportEmailPreview>> GetPendingAsync(string mailboxName, CancellationToken ct)
    {
        var mailboxConfig = _config.Mailboxes.FirstOrDefault(m => m.Name == mailboxName);
        if (mailboxConfig == null)
            throw new ArgumentException($"Mailbox '{mailboxName}' not found");

        var since = DateTime.Now.AddDays(-mailboxConfig.DaysToLookBack);

        await using var mailbox = _mailBoxFactory.Create(mailboxConfig);
        await mailbox.ConnectAsync(ct);
        var headers = await mailbox.ListEmailsAsync(since, ct);
        var previews = new List<ImportEmailPreview>();

        foreach (var header in headers)
        {
            var message = await mailbox.GetEmailAsync(header.MessageId, ct);
            if (message != null)
            {
                previews.Add(new ImportEmailPreview(
                    message.MessageId,
                    message.Subject,
                    message.SenderEmail,
                    message.SenderName,
                    message.ReceivedDate,
                    GetPreviewText(message),
                    _bodySanitizer.Sanitize(message.HtmlBody, message.PlainTextBody),
                    message.Attachments.Count,
                    message.Attachments.Select(a => a.Filename).ToList()
                ));
            }
        }

        return previews;
    }

    public async Task<ImportEmailPreview?> GetPreviewAsync(string mailboxName, string messageId, CancellationToken ct)
    {
        var mailboxConfig = _config.Mailboxes.FirstOrDefault(m => m.Name == mailboxName);
        if (mailboxConfig == null)
            return null;

        await using var mailbox = _mailBoxFactory.Create(mailboxConfig);
        await mailbox.ConnectAsync(ct);
        var message = await mailbox.GetEmailAsync(messageId, ct);

        if (message == null)
            return null;

        return new ImportEmailPreview(
            message.MessageId,
            message.Subject,
            message.SenderEmail,
            message.SenderName,
            message.ReceivedDate,
            GetPreviewText(message),
            _bodySanitizer.Sanitize(message.HtmlBody, message.PlainTextBody),
            message.Attachments.Count,
            message.Attachments.Select(a => a.Filename).ToList()
        );
    }

    public async Task<ImportEmailResult> ImportSingleAsync(string mailboxName, string messageId, bool forceReimport = false, CancellationToken ct = default)
    {
        var mailboxConfig = _config.Mailboxes.FirstOrDefault(m => m.Name == mailboxName);
        if (mailboxConfig == null)
            return new ImportEmailResult(false, EmailImportStatus.Failed, null, $"Mailbox '{mailboxName}' not found", messageId);

        await using var mailbox = _mailBoxFactory.Create(mailboxConfig);
        await mailbox.ConnectAsync(ct);
        var message = await mailbox.GetEmailAsync(messageId, ct);

        if (message == null)
            return new ImportEmailResult(false, EmailImportStatus.Failed, null, "Message not found", messageId);

        return await ImportEmailAsync(mailbox, mailboxConfig, message, forceReimport, ct);
    }

    public async Task<IReadOnlyList<ImportEmailResult>> ImportAllPendingAsync(CancellationToken ct)
    {
        var results = new List<ImportEmailResult>();

        foreach (var mailboxConfig in _config.Mailboxes)
        {
            try
            {
                var since = DateTime.Now.AddDays(-mailboxConfig.DaysToLookBack);

                await using var mailbox = _mailBoxFactory.Create(mailboxConfig);
                await mailbox.ConnectAsync(ct);
                var headers = await mailbox.ListEmailsAsync(since, ct);

                foreach (var header in headers)
                {
                    var message = await mailbox.GetEmailAsync(header.MessageId, ct);
                    if (message != null)
                    {
                        var result = await ImportEmailAsync(mailbox, mailboxConfig, message, false, ct);
                        results.Add(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import from mailbox {MailboxName}", mailboxConfig.Name);
            }
        }

        return results;
    }

    public async Task DeleteAsync(string mailboxName, string messageId, CancellationToken ct)
    {
        var mailboxConfig = _config.Mailboxes.FirstOrDefault(m => m.Name == mailboxName);
        if (mailboxConfig == null)
            throw new ArgumentException($"Mailbox '{mailboxName}' not found");

        await using var mailbox = _mailBoxFactory.Create(mailboxConfig);
        await mailbox.ConnectAsync(ct);
        await mailbox.DeleteEmailAsync(messageId, ct);

        var log = new EmailImportLog
        {
            Id = Guid.CreateVersion7(),
            MailboxName = mailboxName,
            MessageId = messageId,
            Status = EmailImportStatus.Deleted,
            ProcessedOn = DateTime.UtcNow
        };

        _db.Set<EmailImportLog>().Add(log);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ImportEmailResult> ImportEmailAsync(
        IMailBox mailbox,
        ImapConfig mailboxConfig,
        MailMessage message,
        bool forceReimport,
        CancellationToken ct)
    {

        try
        {
            if (!forceReimport)
            {
                var existingLog = await _db.Set<EmailImportLog>()
                    .FirstOrDefaultAsync(l => l.MessageId == message.MessageId, ct);

                if (existingLog != null)
                {
                    _logger.LogWarning("Message {MessageId} already processed", message.MessageId);
                    return new ImportEmailResult(false, existingLog.Status, existingLog.ServiceRequestId, "Already processed", message.MessageId);
                }
            }

            var groupResult = await _groupResolver.ResolveGroupAsync(mailboxConfig, message.SenderEmail, ct);

            if (!groupResult.IsSuccess)
            {
                await mailbox.MoveToFolderAsync(message.MessageId, mailboxConfig.QuarantineFolder, ct);
                await LogAsync(message, mailboxConfig.Name, EmailImportStatus.Quarantined, null, null, ct);
                return new ImportEmailResult(false, EmailImportStatus.Quarantined, null, "Unknown sender and no default group configured", message.MessageId);
            }

            var groupId = groupResult.Value.GroupId;
            var userId = groupResult.Value.UserId;

            var description = _bodySanitizer.Sanitize(message.HtmlBody, message.PlainTextBody);

            // create as draft
            var cmd = new CreateServiceRequestCommand(groupId, "email-import", new RequestDescription
            {
                Title = message.Subject,
                Text = description
            }, null, userId);

            var result = await _mediator.Send(cmd, ct);

            var srId = result.Id;

            // attach images
            foreach (var attachment in message.Attachments)
            {
                var sanitizedFilename = _filenameSanitizer.Sanitize(attachment.Filename);
                await ImportAttachmentAsync(srId, attachment, sanitizedFilename, CancellationToken.None);
            }

            // publish request
            var upd = new UpdateServiceRequestCommand(srId, IsDraft: false);
            await _mediator.Send(upd, CancellationToken.None);

            // delete email
            if (mailboxConfig.DeleteAfterImport)
            {
                await mailbox.DeleteEmailAsync(message.MessageId, ct);
            }
            await LogAsync(message, mailboxConfig.Name, EmailImportStatus.Imported, srId, null, ct, userId);

            return new ImportEmailResult(true, EmailImportStatus.Imported, srId, null, message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import message {MessageId}", message.MessageId);
            await LogAsync(message, mailboxConfig.Name, EmailImportStatus.Failed, null, ex.Message, ct);
            return new ImportEmailResult(false, EmailImportStatus.Failed, null, ex.Message, message.MessageId);
        }
    }

    private async Task ImportAttachmentAsync(Guid serviceRequestId, MailAttachment attachment, string filename, CancellationToken ct)
    {
        var cmd = new UploadDocumentCommand(
            serviceRequestId,
            null,
            filename,
            attachment.Size,
            attachment.Content,
            attachment.ContentType);

        await _mediator.Send(cmd, ct);
    }

    private async Task LogAsync(
        MailMessage message,
        string mailboxName,
        EmailImportStatus status,
        Guid? serviceRequestId,
        string? errorMessage,
        CancellationToken ct,
        Guid? userId = null)
    {
        var log = new EmailImportLog
        {
            Id = Guid.CreateVersion7(),
            MailboxName = mailboxName,
            MessageId = message.MessageId,
            SenderEmail = message.SenderEmail,
            SenderName = message.SenderName,
            Subject = message.Subject,
            UserId = userId,
            ServiceRequestId = serviceRequestId,
            Status = status,
            ErrorMessage = errorMessage,
            ProcessedOn = DateTime.UtcNow
        };

        _db.Set<EmailImportLog>().Add(log);
        await _db.SaveChangesAsync(ct);
    }

    private static string? GetPreviewText(MailMessage message)
    {
        var text = !string.IsNullOrWhiteSpace(message.PlainTextBody)
            ? message.PlainTextBody
            : message.HtmlBody;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Length > 500 ? text.Substring(0, 500) + "..." : text;
    }
}