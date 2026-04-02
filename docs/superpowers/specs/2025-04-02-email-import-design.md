# Email Import Feature Design

**Date:** 2025-04-02  
**Status:** Draft  
**Branch:** feature/email-import

## Overview

Add functionality to import emails from configured IMAP mailboxes into ServiceRequest resources. This enables users to create ServiceRequests by sending emails to monitored mailboxes.

## Sprint 1 Scope

- Backend infrastructure (entities, services, worker)
- API endpoints for admin operations
- IMAP client implementation
- Background worker for automatic import
- **Admin UI deferred to Sprint 2**

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Configuration                                 │
│  EmailImport: { Enabled, Interval, Mailboxes[{Name, IMAP,creds}] } │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                v
┌─────────────────────────────────────────────────────────────────────┐
│                    EmailImportWorker (BackgroundService)            │
│  - Runs every N minutes (configurable)                             │
│  - For each configured mailbox:                                    │
│      - Connect via IEmailImportClient                              │
│      - Process pending emails                                      │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                v
┌─────────────────────────────────────────────────────────────────────┐
│                    IEmailImportService                              │
│  - ImportSingleAsync(mailboxName, messageId)                      │
│  - ImportAllPendingAsync()                                         │
│  - GetPendingAsync(mailboxName)                                   │
│  - GetPreviewAsync(mailboxName, messageId)                        │
│  - DeleteAsync(mailboxName, messageId)                            │
└─────────────────────────────────────────────────────────────────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        v                       v                       v
┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐
│IEmailImportClient│   │IEmailImportGroup │   │ ServiceRequest  │
│ (IMAP operations)│   │    Resolver      │   │ + Documents     │
└─────────────────┘   └─────────────────┘   └─────────────────┘
        │
        v
┌─────────────────┐
│  Mail Server    │
│  (IMAP)         │
│  - Inbox        │
│  - Quarantine   │
└─────────────────┘
```

## Configuration

### appsettings.json

```json
{
  "EmailImport": {
    "Enabled": true,
    "IntervalMinutes": 5,
    "MaxAttachmentSizeMB": 50,
    "Mailboxes": [
      {
        "Name": "Main Support",
        "ImapServer": "imap.gmail.com",
        "ImapPort": 993,
        "Username": "support@domain.com",
        "Password": "app-specific-password",
        "DefaultGroupId": "guid-here",
        "QuarantineFolder": "Quarantine"
      },
      {
        "Name": "Priority",
        "ImapServer": "imap.example.com",
        "ImapPort": 993,
        "Username": "priority@domain.com",
        "Password": "secret",
        "DefaultGroupId": null
      }
    ]
  }
}
```

### Configuration POCOs

```csharp
public class EmailImportConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 5;
    public int MaxAttachmentSizeMB { get; set; } = 50;
    public List<ImportMailboxConfig> Mailboxes { get; set; } = [];
}

public class ImportMailboxConfig
{
    public string Name { get; set; } = string.Empty;
    public string ImapServer { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Guid? DefaultGroupId { get; set; }
    public string QuarantineFolder { get; set; } = "Quarantine";
}
```

## Domain Entities

### EmailImportLog

Audit trail for all email import attempts.

```csharp
public class EmailImportLog : BaseEntity
{
    public string MailboxName { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public string? Subject { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ServiceRequestId { get; set; }
    public EmailImportStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedOn { get; set; }
}

public enum EmailImportStatus
{
    Pending = 0,
    Imported = 1,
    Skipped = 2,      // Known user, no group configured
    Quarantined = 3,  // Unknown sender
    Failed = 4,       // Error during import
    Deleted = 5       // Manually deleted via API
}
```

### EmailImportSettings (Value Object in UserProfile)

```csharp
public class EmailImportSettings
{
    public Guid? DefaultGroupId { get; set; }
    // Future: public bool AutoImportEnabled { get; set; } = true;
}
```

**Modification to UserProfile:**

```csharp
public partial class UserProfile
{
    // Existing properties...
    public EmailImportSettings? EmailImportSettings { get; set; }
}
```

## Contracts (Application Layer)

### IEmailImportClient

IMAP operations for a single mailbox.

```csharp
public interface IEmailImportClient : IDisposable
{
    string MailboxName { get; }
    Task ConnectAsync(CancellationToken ct);
    Task<IReadOnlyList<EmailMessage>> GetPendingAsync(CancellationToken ct);
    Task<EmailMessage?> GetAsync(string messageId, CancellationToken ct);
    Task MoveToQuarantineFolderAsync(string messageId, CancellationToken ct);
    Task DeleteAsync(string messageId, CancellationToken ct);
}
```

### IEmailImportService

Business logic for email import.

```csharp
public interface IEmailImportService
{
    Task<IReadOnlyList<MailboxSummary>> GetMailboxesAsync(CancellationToken ct);
    Task<IReadOnlyList<EmailPreview>> GetPendingAsync(string mailboxName, CancellationToken ct);
    Task<EmailPreview?> GetPreviewAsync(string mailboxName, string messageId, CancellationToken ct);
    Task<EmailImportResult> ImportSingleAsync(string mailboxName, string messageId, CancellationToken ct);
    Task<IReadOnlyList<EmailImportResult>> ImportAllPendingAsync(CancellationToken ct);
    Task DeleteAsync(string mailboxName, string messageId, CancellationToken ct);
}
```

### IEmailImportGroupResolver

Strategy for determining which Group a ServiceRequest should be created in.

```csharp
public interface IEmailImportGroupResolver
{
    Task<(Guid GroupId, Guid? UserId)?> ResolveGroupAsync(
        string mailboxName,
        string senderEmail,
        CancellationToken ct);
}
```

**Default Implementation Priority:**
1. Match `User.Email` to sender
2. If found → use `User.Profile.EmailImportSettings.DefaultGroupId`
3. If not set → use `ImportMailboxConfig.DefaultGroupId`
4. If still not set → return null (skip import)

### IEmailBodyTextSanitizer

Converts email body to ServiceRequest description text.

```csharp
public interface IEmailBodyTextSanitizer
{
    string Sanitize(string? htmlBody, string? plainTextBody);
}
```

**Sprint 1 Behavior:**
- Prefer plain text if available
- Strip HTML tags if only HTML available
- Future: Allow safe HTML subset

### IEmailAttachmentNameSanitizer

Sanitizes attachment filenames.

```csharp
public interface IEmailAttachmentNameSanitizer
{
    string Sanitize(string filename);
}
```

**Sprint 1 Behavior:**
- Return filename as-is
- Future: Remove path traversal, invalid chars, etc.

## DTOs

```csharp
public record EmailMessage(
    string MessageId,
    string Subject,
    string SenderEmail,
    string? SenderName,
    string? HtmlBody,
    string? PlainTextBody,
    DateTime ReceivedDate,
    IReadOnlyList<EmailAttachment> Attachments
);

public record EmailAttachment(
    string Filename,
    string ContentType,
    long Size,
    Stream Content
);

public record EmailPreview(
    string MessageId,
    string Subject,
    string SenderEmail,
    string? SenderName,
    DateTime ReceivedDate,
    string? PreviewText,
    int AttachmentCount,
    IReadOnlyList<string> AttachmentNames
);

public record EmailImportResult(
    bool Success,
    EmailImportStatus Status,
    string? ServiceRequestId,
    string? ErrorMessage,
    string MessageId
);

public record MailboxSummary(
    string Name,
    int PendingCount,
    DateTime? LastChecked
);
```

## Import Flow

```
EmailImportWorker (every N minutes)
    │
    v
For each configured Mailbox:
    │
    v
Connect via IEmailImportClient
    │
    v
GetPendingAsync() → List<EmailMessage>
    │
    v
For each EmailMessage:
    │
    v
Check EmailImportLog for existing MessageId
    │
    ├─► If exists → Skip (already processed)
    │
    v
Match sender email to User.Email
    │
    ├─► Found + has EmailImportSettings.GroupId → Import
    │
    ├─► Found + no GroupId + Mailbox.DefaultGroupId → Import with default
    │
    ├─► Found + no GroupId + no Default → Skip (log warning)
    │
    └─► Not found → Move to Quarantine folder, log
    │
    v
Import:
    - Create ServiceRequest
    - Extract body (HTML → strip tags for description)
    - Download attachments → Create DocumentNodes
    - Log success to EmailImportLog
```

## Background Worker

```csharp
public class EmailImportWorker : BackgroundService
{
    private readonly EmailImportConfig _config;
    private readonly ILogger<EmailImportWorker> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_config.Enabled)
            {
                try
                {
                    await ImportAllMailboxesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during email import");
                }
            }
            
            await Task.Delay(TimeSpan.FromMinutes(_config.IntervalMinutes), stoppingToken);
        }
    }
}
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/email-import/mailboxes` | List configured mailboxes |
| GET | `/api/admin/email-import/{mailbox}/pending` | List pending emails |
| GET | `/api/admin/email-import/{mailbox}/{messageId}/preview` | Preview email |
| POST | `/api/admin/email-import/{mailbox}/{messageId}/import` | Import single email |
| DELETE | `/api/admin/email-import/{mailbox}/{messageId}` | Delete email |
| POST | `/api/admin/email-import/import-all` | Trigger bulk import |
| GET | `/api/admin/email-import/logs` | View import logs |

## File Structure

```
src/
├── core/
│   ├── iPath.Domain/
│   │   └── Entities/
│   │       └── EmailImport/
│   │           └── EmailImportLog.cs
│   │
│   └── iPath.Application/
│       ├── Contracts/
│       │   ├── IEmailImportClient.cs
│       │   ├── IEmailImportService.cs
│       │   ├── IEmailImportGroupResolver.cs
│       │   ├── IEmailBodyTextSanitizer.cs
│       │   └── IEmailAttachmentNameSanitizer.cs
│       │
│       └── Features/
│           └── EmailImport/
│               ├── EmailMessage.cs
│               ├── EmailPreview.cs
│               ├── EmailImportConfig.cs
│               ├── EmailImportSettings.cs
│               └── Commands/
│                   └── ImportEmailCommand.cs
│
├── infrastructure/
│   ├── iPath.Database.EFCore/
│   │   └── Migrations/ (new migration)
│   │
│   ├── iPath.Mail/ (new project) or iPath.Google/
│   │   └── Email/
│   │       ├── ImapEmailImportClient.cs
│   │       └── EmailImportClientFactory.cs
│   │
│   └── iPath.API/
│       ├── Services/
│       │   └── Email/
│       │       ├── EmailImportWorker.cs
│       │       ├── EmailImportService.cs
│       │       ├── EmailImportGroupResolver.cs
│       │       ├── EmailBodyTextSanitizer.cs
│       │       └── EmailAttachmentNameSanitizer.cs
│       │
│       └── Endpoints/
│           └── EmailImportEndpoints.cs
│
└── appsettings.json (EmailImport section)
```

## Implementation Order

1. Domain entities - `EmailImportLog`, `EmailImportSettings`
2. Configuration POCOs and `appsettings` schema
3. Interfaces - All contracts in Application layer
4. IMAP client - `ImapEmailImportClient` using MailKit
5. Sanitizers - Body and filename sanitizers
6. Group resolver - Default implementation
7. Import service - Core business logic
8. Background worker - `EmailImportWorker`
9. API endpoints - Admin endpoints
10. DI registration - `APIServicesRegistration`
11. Database migration - Add `EmailImportLog` table

## Edge Cases

| Case | Behavior |
|------|----------|
| Duplicate MessageId | Skip, check `EmailImportLog` first |
| Unknown sender | Move to Quarantine folder, log `Status=Quarantined` |
| No group configured | Log `Status=Skipped`, don't import |
| IMAP connection failure | Log error, retry next interval |
| Attachment too large | Skip attachment, log warning in `EmailImportLog` |
| Failed import | Log `Status=Failed`, don't delete from inbox |

## Future Considerations (Post Sprint 1)

1. **Admin UI** - Web interface for managing email imports
2. **HTML body support** - Allow safe HTML subset in descriptions
3. **Filename sanitization** - Remove path traversal, invalid chars
4. **Per-user toggle** - `EmailImportSettings.AutoImportEnabled`
5. **Quarantine auto-delete** - Auto-delete quarantined emails after N days
6. **Gmail API** - Alternative to IMAP for Google Workspace