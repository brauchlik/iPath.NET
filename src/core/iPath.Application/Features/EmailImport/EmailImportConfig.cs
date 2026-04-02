namespace iPath.Application.Features.EmailImport;

public class EmailImportConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 5;
    public int MaxAttachmentSizeMB { get; set; } = 50;
    public List<ImapConfig> Mailboxes { get; set; } = [];
}
