namespace iPath.Application.Features.EmailImport;

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