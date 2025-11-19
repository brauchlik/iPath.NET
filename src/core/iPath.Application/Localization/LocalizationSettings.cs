namespace iPath.Application.Localization;

public class LocalizationSettings
{
    public const string ConfigName = "LocalizationSettings";

    public bool Active { get; set; }
    public bool AddMissingStrings { get; set; }
    public bool AutoSave { get; set; }
    public string? LocalesRoot { get; set; }


    public string[] SupportedCultures = ["en", "de", "fr", "it"];
}
