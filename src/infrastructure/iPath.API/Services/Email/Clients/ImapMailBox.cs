using iPath.Application.Contracts;
using iPath.Application.Features.EmailImport;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace iPath.API.Services.Email.Clients;

public class ImapMailBox : IMailBox
{
    private readonly ImapConfig _config;
    private ImapClient? _client;
    private IMailFolder? _inbox;

    public string MailboxName => _config.Name;

    public ImapMailBox(ImapConfig config)
    {
        _config = config;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _client = new ImapClient();
        await _client.ConnectAsync(_config.ImapServer, _config.ImapPort, true, ct);
        _client.AuthenticationMechanisms.Remove("XOAUTH2");
        await _client.AuthenticateAsync(_config.Username, _config.Password, ct);
        _inbox = _client.Inbox;
        await _inbox.OpenAsync(FolderAccess.ReadWrite, ct);
    }

    public async Task<IReadOnlyList<MailHeader>> ListEmailsAsync(DateTime? since = null, CancellationToken ct = default)
    {
        EnsureConnected();
        var headers = new List<MailHeader>();

        SearchQuery query = since.HasValue 
            ? SearchQuery.SentSince(since.Value)
            : SearchQuery.All;

        var results = await _inbox!.SearchAsync(SearchOptions.All, query, ct);
        
        foreach (var uniqueId in results.UniqueIds)
        {
            var message = await _inbox.GetMessageAsync(uniqueId, ct);
            var sender = message.From.FirstOrDefault();
            
            headers.Add(new MailHeader(
                message.MessageId ?? uniqueId.ToString(),
                message.Subject ?? "",
                sender is MailboxAddress mailbox ? mailbox.Address : sender?.Name ?? "",
                sender?.Name,
                message.Date.DateTime,
                message.Attachments.Any()
            ));
        }

        return headers;
    }

    public async Task<MailMessage?> GetEmailAsync(string messageId, CancellationToken ct = default)
    {
        EnsureConnected();
        
        var results = await _inbox!.SearchAsync(SearchQuery.HeaderContains("Message-ID", messageId), ct);
        if (results.Count == 0)
            return null;

        var message = await _inbox.GetMessageAsync(results[0], ct);
        return await MapToMailMessageAsync(message, results[0], ct);
    }

    public async Task MoveToFolderAsync(string messageId, string folderName, CancellationToken ct = default)
    {
        EnsureConnected();

        var folder = await GetOrCreateFolderAsync(folderName, ct);
        var results = await _inbox!.SearchAsync(SearchQuery.HeaderContains("Message-ID", messageId), ct);
        
        if (results.Count > 0)
        {
            await _inbox.MoveToAsync(results[0], folder, ct);
        }
    }

    public async Task DeleteEmailAsync(string messageId, CancellationToken ct = default)
    {
        EnsureConnected();

        var results = await _inbox!.SearchAsync(SearchQuery.HeaderContains("Message-ID", messageId), ct);
        
        if (results.Count > 0)
        {
            await _inbox.AddFlagsAsync(results[0], MessageFlags.Deleted, true, ct);
            await _inbox.ExpungeAsync(ct);
        }
    }

    public async Task EnsureFolderExistsAsync(string folderName, CancellationToken ct = default)
    {
        EnsureConnected();
        await GetOrCreateFolderAsync(folderName, ct);
    }

    private async Task<IMailFolder> GetOrCreateFolderAsync(string folderName, CancellationToken ct)
    {
        EnsureConnected();
        
        var personal = _client!.GetFolder(_client.PersonalNamespaces[0]);
        await personal.OpenAsync(FolderAccess.ReadWrite, ct);
        
        var subfolders = await personal.GetSubfoldersAsync(false, ct);
        var folder = subfolders.FirstOrDefault(f => f.Name == folderName);
        
        if (folder == null)
        {
            folder = await personal.CreateAsync(folderName, true, ct);
        }

        return folder;
    }

    private async Task<MailMessage> MapToMailMessageAsync(MimeMessage message, UniqueId uniqueId, CancellationToken ct)
    {
        var attachments = new List<MailAttachment>();

        foreach (var attachment in message.Attachments)
        {
            if (attachment is MimePart mimePart)
            {
                var memory = new MemoryStream();
                mimePart.Content.DecodeTo(memory);
                memory.Position = 0;

                attachments.Add(new MailAttachment(
                    mimePart.FileName ?? "attachment",
                    mimePart.ContentType?.MimeType ?? "application/octet-stream",
                    memory.Length,
                    memory
                ));
            }
        }

        var sender = message.From.FirstOrDefault();
        var senderEmail = sender is MailboxAddress mailbox ? mailbox.Address : sender?.Name ?? "";
        
        return new MailMessage(
            message.MessageId ?? uniqueId.ToString(),
            message.Subject ?? "",
            senderEmail,
            sender?.Name,
            message.HtmlBody,
            message.TextBody,
            message.Date.DateTime,
            attachments
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

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisconnectAsync(true);
            _client.Dispose();
        }
    }
}

public class ImapMailBoxFactory : IMailBoxFactory
{
    public IMailBox Create(ImapConfig config)
    {
        return new ImapMailBox(config);
    }
}