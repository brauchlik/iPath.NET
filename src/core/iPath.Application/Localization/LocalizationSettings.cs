namespace iPath.Application.Localization;

public class LocalizationSettings
{
    public const string ConfigName = "LocalizationSettings";

    public bool Active { get; set; }
    public bool AddMissingStrings { get; set; }
    public bool AutoSave { get; set; }
    public string? LocalesRoot { get; set; }

    public string[] SupportedCultures { get; set; } = ["en", "de", "fr", "it"];

    public Dictionary<string, string> CultureDisplayNames { get; set; } = new()
    {
        ["en"] = "English",
        ["de"] = "Deutsch",
        ["fr"] = "Français",
        ["it"] = "Italiano"
    };
}
