namespace iPath.Application.Features.EmailImport;

public class ImapConfig
{
    public string Name { get; set; } = string.Empty;
    public string ImapServer { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public int DaysToLookBack { get; set; } = 10;
    public Guid? DefaultGroupId { get; set; }
    public string QuarantineFolder { get; set; } = "Quarantine";

    public bool DeleteAfterImport { get; set; } = true;
}
