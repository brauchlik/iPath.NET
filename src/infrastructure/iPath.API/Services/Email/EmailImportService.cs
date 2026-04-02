using iPath.Application;
using iPath.Application.Contracts;
using iPath.Application.Features.Documents;
using iPath.Application.Features.EmailImport;
using iPath.Application.Features.ServiceRequests;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Email;

public class EmailImportService : IEmailImportService
{
    private readonly iPathDbContext _db;
    private readonly IEmailImportClientFactory _clientFactory;
    private readonly IEmailImportGroupResolver _groupResolver;
    private readonly IEmailBodyTextSanitizer _bodySanitizer;
    private readonly IEmailAttachmentNameSanitizer _filenameSanitizer;
    private readonly IMediator _mediator;
    private readonly EmailImportConfig _config;
    private readonly ILogger<EmailImportService> _logger;

    public EmailImportService(
        iPathDbContext db,
        IEmailImportClientFactory clientFactory,
        IEmailImportGroupResolver groupResolver,
        IEmailBodyTextSanitizer bodySanitizer,
        IEmailAttachmentNameSanitizer filenameSanitizer,
        IMediator mediator,
        IOptions<EmailImportConfig> config,
        ILogger<EmailImportService> logger)
    {
        _db = db;
        _clientFactory = clientFactory;
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
                using var client = _clientFactory.Create(mailboxConfig);
                await client.ConnectAsync(ct);
                var pending = await client.GetPendingAsync(ct);

                var lastLog = await _db.Set<EmailImportLog>()
                    .Where(l => l.MailboxName == mailboxConfig.Name)
                    .OrderByDescending(l => l.ProcessedOn)
                    .FirstOrDefaultAsync(ct);

                summaries.Add(new ImportMailboxSummary(
                    mailboxConfig.Name,
                    pending.Count,
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

        using var client = _clientFactory.Create(mailboxConfig);
        await client.ConnectAsync(ct);
        var messages = await client.GetPendingAsync(ct);

        return messages.Select(m => new ImportEmailPreview(
            m.MessageId,
            m.Subject,
            m.SenderEmail,
            m.SenderName,
            m.ReceivedDate,
            GetPreviewText(m),
            m.Attachments.Count,
            m.Attachments.Select(a => a.Filename).ToList()
        )).ToList();
    }

    public async Task<ImportEmailPreview?> GetPreviewAsync(string mailboxName, string messageId, CancellationToken ct)
    {
        var mailboxConfig = _config.Mailboxes.FirstOrDefault(m => m.Name == mailboxName);
        if (mailboxConfig == null)
            return null;

        using var client = _clientFactory.Create(mailboxConfig);
        await client.ConnectAsync(ct);
        var message = await client.GetAsync(messageId, ct);

        if (message == null)
            return null;

        return new ImportEmailPreview(
            message.MessageId,
            message.Subject,
            message.SenderEmail,
            message.SenderName,
            message.ReceivedDate,
            GetPreviewText(message),
            message.Attachments.Count,
            message.Attachments.Select(a => a.Filename).ToList()
        );
    }

    public async Task<ImportEmailResult> ImportSingleAsync(string mailboxName, string messageId, CancellationToken ct)
    {
        var mailboxConfig = _config.Mailboxes.FirstOrDefault(m => m.Name == mailboxName);
        if (mailboxConfig == null)
            return new ImportEmailResult(false, EmailImportStatus.Failed, null, $"Mailbox '{mailboxName}' not found", messageId);

        using var client = _clientFactory.Create(mailboxConfig);
        await client.ConnectAsync(ct);
        var message = await client.GetAsync(messageId, ct);

        if (message == null)
            return new ImportEmailResult(false, EmailImportStatus.Failed, null, "Message not found", messageId);

        return await ImportEmailAsync(client, mailboxConfig, message, ct);
    }

    public async Task<IReadOnlyList<ImportEmailResult>> ImportAllPendingAsync(CancellationToken ct)
    {
        var results = new List<ImportEmailResult>();

        foreach (var mailboxConfig in _config.Mailboxes)
        {
            try
            {
                using var client = _clientFactory.Create(mailboxConfig);
                await client.ConnectAsync(ct);
                var messages = await client.GetPendingAsync(ct);

                foreach (var message in messages)
                {
                    var result = await ImportEmailAsync(client, mailboxConfig, message, ct);
                    results.Add(result);
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

        using var client = _clientFactory.Create(mailboxConfig);
        await client.ConnectAsync(ct);
        await client.DeleteAsync(messageId, ct);

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
        IEmailImportClient client,
        ImportMailboxConfig mailboxConfig,
        ImportEmailMessage message,
        CancellationToken ct)
    {
        var existingLog = await _db.Set<EmailImportLog>()
            .FirstOrDefaultAsync(l => l.MessageId == message.MessageId, ct);

        if (existingLog != null)
        {
            _logger.LogWarning("Message {MessageId} already processed", message.MessageId);
            return new ImportEmailResult(false, existingLog.Status, existingLog.ServiceRequestId, "Already processed", message.MessageId);
        }

        var groupResult = await _groupResolver.ResolveGroupAsync(mailboxConfig.Name, message.SenderEmail, ct);

        Guid? userId = null;
        Guid groupId;

        if (groupResult == null)
        {
            if (mailboxConfig.DefaultGroupId != null)
            {
                groupId = mailboxConfig.DefaultGroupId.Value;
            }
            else
            {
                await client.MoveToQuarantineFolderAsync(message.MessageId, ct);
                await LogAsync(message, mailboxConfig.Name, EmailImportStatus.Quarantined, null, null, ct);
                return new ImportEmailResult(false, EmailImportStatus.Quarantined, null, "Unknown sender moved to quarantine", message.MessageId);
            }
        }
        else
        {
            groupId = groupResult.Value.GroupId;
            userId = groupResult.Value.UserId;
        }

        try
        {
            var description = _bodySanitizer.Sanitize(message.HtmlBody, message.PlainTextBody);
            
            var cmd = new CreateServiceRequestCommand(groupId, "email-import", new RequestDescription
            {
                Title = message.Subject,
                Text = description
            }, null);

            var result = await _mediator.Send(cmd, ct);

            var srId = result.Id;

            foreach (var attachment in message.Attachments)
            {
                var sanitizedFilename = _filenameSanitizer.Sanitize(attachment.Filename);
                await ImportAttachmentAsync(srId, attachment, sanitizedFilename, ct);
            }

            await client.DeleteAsync(message.MessageId, ct);
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

    private async Task ImportAttachmentAsync(Guid serviceRequestId, ImportEmailAttachment attachment, string filename, CancellationToken ct)
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
        ImportEmailMessage message,
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

    private static string? GetPreviewText(ImportEmailMessage message)
    {
        var text = !string.IsNullOrWhiteSpace(message.PlainTextBody)
            ? message.PlainTextBody
            : message.HtmlBody;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Length > 500 ? text.Substring(0, 500) + "..." : text;
    }
}