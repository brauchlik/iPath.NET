namespace iPath.Application.Localization;

public class LocalizationSettings
{
    public const string ConfigName = "LocalizationSettings";

    /// <summary>
    /// The live translation store this instance reads/writes at runtime. Defaults to the same
    /// folder as <see cref="DefaultsRoot"/>, but a deployment that wants a human editor's
    /// translations to survive across redeploys should point this at a separate, persistent
    /// location (same pattern as iPathConfig.DataProtectionKeysPath).
    /// </summary>
    public string LocalesRoot { get; set; } = "./Locales";

    /// <summary>
    /// The read-only baseline shipped with the app itself - always the repo's tracked
    /// Locales/ folder, published like any other content file. Not meant to be redirected per
    /// deployment; only <see cref="LocalesRoot"/> should be overridden.
    /// </summary>
    public string DefaultsRoot { get; set; } = "./Locales";

    /// <summary>
    /// When true, on startup the app pushes new/updated keys from <see cref="DefaultsRoot"/>
    /// into <see cref="LocalesRoot"/> - additive only, never overwrites an already-translated
    /// key. Default false, opt-in like iPathConfig.DbAutoMigrate.
    /// </summary>
    public bool AutoUpdate { get; set; }

    public string[] SupportedCultures { get; set; } = [];

    public Dictionary<string, string> CultureDisplayNames { get; set; } = new();

    /// <summary>
    /// True unless <see cref="DefaultsRoot"/> and <see cref="LocalesRoot"/> resolve to the same
    /// folder. When they are the same, the shipped baseline *is* the live store, so the admin
    /// editor must stay read-only - writes would modify the shipped files and be lost on redeploy.
    /// </summary>
    public bool IsLiveStoreSeparate => !PathsEqual(LocalesRoot, DefaultsRoot);

    private static bool PathsEqual(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), comparison);
    }
}
