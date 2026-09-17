namespace iPath.Application.Localization;

public class LocalizationSettings
{
    public const string ConfigName = "LocalizationSettings";

    public bool Active { get; set; }
    public bool AddMissingStrings { get; set; }
    public bool AutoSave { get; set; }
    public string LocalesRoot { get; set; } = "./Locales";

    public string[] SupportedCultures { get; set; } = [];

    public Dictionary<string, string> CultureDisplayNames { get; set; } = new();
}
