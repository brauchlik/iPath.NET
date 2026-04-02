using iPath.Application.Contracts;
using iPath.Application.Features.EmailImport;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace iPath.Google.Email;

public class ImapEmailImportClient : IEmailImportClient
{
    private readonly ImportMailboxConfig _config;
    private ImapClient? _client;
    private IMailFolder? _inbox;

    public string MailboxName => _config.Name;

    public ImapEmailImportClient(ImportMailboxConfig config)
    {
        _config = config;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _client = new ImapClient();
        await _client.ConnectAsync(_config.ImapServer, _config.ImapPort, true, ct);
        _client.AuthenticationMechanisms.Remove("XOAUTH2");
        await _client.AuthenticateAsync(_config.Username, _config.Password, ct);
        _inbox = _client.Inbox;
        await _inbox.OpenAsync(FolderAccess.ReadWrite, ct);
    }

    public async Task<IReadOnlyList<ImportEmailMessage>> GetPendingAsync(CancellationToken ct)
    {
        EnsureConnected();
        var messages = new List<ImportEmailMessage>();

        var results = await _inbox!.SearchAsync(SearchOptions.All, SearchQuery.NotSeen, ct);
        
        foreach (var uniqueId in results.UniqueIds)
        {
            var message = await _inbox.GetMessageAsync(uniqueId, ct);
            var emailMessage = await MapMessageAsync(message, uniqueId, ct);
            messages.Add(emailMessage);
        }

        return messages;
    }

    public async Task<ImportEmailMessage?> GetAsync(string messageId, CancellationToken ct)
    {
        EnsureConnected();
        
        var results = await _inbox!.SearchAsync(SearchQuery.HeaderContains("Message-ID", messageId), ct);
        if (results.Count == 0)
            return null;

        var message = await _inbox.GetMessageAsync(results[0], ct);
        return await MapMessageAsync(message, results[0], ct);
    }

    public async Task MoveToQuarantineFolderAsync(string messageId, CancellationToken ct)
    {
        EnsureConnected();

        var quarantineFolder = await GetOrCreateQuarantineFolderAsync(ct);
        var results = await _inbox!.SearchAsync(SearchQuery.HeaderContains("Message-ID", messageId), ct);
        
        if (results.Count > 0)
        {
            await _inbox.MoveToAsync(results[0], quarantineFolder, ct);
        }
    }

    public async Task DeleteAsync(string messageId, CancellationToken ct)
    {
        EnsureConnected();

        var results = await _inbox!.SearchAsync(SearchQuery.HeaderContains("Message-ID", messageId), ct);
        
        if (results.Count > 0)
        {
            await _inbox.AddFlagsAsync(results[0], MessageFlags.Deleted, true, ct);
            await _inbox.ExpungeAsync(ct);
        }
    }

    private async Task<IMailFolder> GetOrCreateQuarantineFolderAsync(CancellationToken ct)
    {
        EnsureConnected();
        
        var personal = _client!.GetFolder(_client.PersonalNamespaces[0]);
        await personal.OpenAsync(FolderAccess.ReadWrite, ct);
        
        var subfolders = await personal.GetSubfoldersAsync(false, ct);
        var quarantineFolder = subfolders.FirstOrDefault(f => f.Name == _config.QuarantineFolder);
        
        if (quarantineFolder == null)
        {
            quarantineFolder = await personal.CreateAsync(_config.QuarantineFolder, true, ct);
        }

        return quarantineFolder;
    }

    private async Task<ImportEmailMessage> MapMessageAsync(MimeMessage message, UniqueId uniqueId, CancellationToken ct)
    {
        var attachments = new List<ImportEmailAttachment>();

        foreach (var attachment in message.Attachments)
        {
            if (attachment is MimePart mimePart)
            {
                using var memory = new MemoryStream();
                await mimePart.Content.WriteToAsync(memory, ct);
                memory.Position = 0;

                attachments.Add(new ImportEmailAttachment(
                    mimePart.FileName ?? "attachment",
                    mimePart.ContentType?.MimeType ?? "application/octet-stream",
                    memory.Length,
                    memory
                ));
            }
        }

        var sender = message.From.FirstOrDefault();
        var senderEmail = sender is MailboxAddress mailbox ? mailbox.Address : sender?.Name ?? "";
        
        return new ImportEmailMessage(
            MessageId: message.MessageId ?? uniqueId.ToString(),
            Subject: message.Subject ?? "",
            SenderEmail: senderEmail,
            SenderName: sender?.Name,
            HtmlBody: message.HtmlBody,
            PlainTextBody: message.TextBody,
            ReceivedDate: message.Date.DateTime,
            Attachments: attachments
        );
    }

    private void EnsureConnected()
    {
        if (_client == null || !_client.IsConnected)
            throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");
    }

    public void Dispose()
    {
        _client?.Disconnect(true);
        _client?.Dispose();
    }
}

public class ImapEmailImportClientFactory : IEmailImportClientFactory
{
    public IEmailImportClient Create(ImportMailboxConfig config)
    {
        return new ImapEmailImportClient(config);
    }
}