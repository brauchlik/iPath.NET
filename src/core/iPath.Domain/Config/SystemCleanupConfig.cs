namespace iPath.Domain.Config;

public class SystemCleanupConfig
{
    public const string ConfigName = "SystemCleanup";

    public bool Enabled { get; set; } = true;
    public string TimeOfDay { get; set; } = "03:00";
    public bool PurgeDeletedDocuments { get; set; } = false;
    public bool CleanStaleCache { get; set; } = true;
    public int StaleCacheDays { get; set; } = 7;
    public bool CleanStaging { get; set; } = true;
    public int StaleStagingDays { get; set; } = 2;
}
