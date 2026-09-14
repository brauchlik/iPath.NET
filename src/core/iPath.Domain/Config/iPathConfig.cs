using Humanizer;

namespace iPath.Domain.Config;

public class iPathConfig
{
    public const string ConfigName = "iPathConfig";

    public bool DbSeedingActive { get; set; }
    public bool DbAutoMigrate { get; set; }

    /// <summary>
    /// When set, DbSeeder uses this instead of generating a random Admin password.
    /// Only ever set by the e2e test config — leave unset everywhere else.
    /// </summary>
    public string? FixedAdminPassword { get; set; }

    public string DataRoot { get; set; } = string.Empty;
    public string TempDataPath { get; set; } = string.Empty;
    public string LocalDataPath { get; set; } = string.Empty;
    public string FhirResourceFilePath { get; set; } = string.Empty;

    public bool ExportNodeJson { get; set; }

    public string ReverseProxyAddresse { get; set; }

    /// <summary>
    /// Filesystem path where ASP.NET Core DataProtection keys are persisted.
    /// When unset, keys fall back to an in-memory store, which invalidates
    /// all user sessions (cookies, antiforgery tokens) on every restart.
    /// Recommended: a directory under the app data root, e.g. /opt/ipath/keys.
    /// </summary>
    public string? DataProtectionKeysPath { get; set; }
}


public class iPathClientConfig
{
    public const string ConfigName = "iPathClientConfig";

    public string? BaseAddress { get; set; } = null;
    public int ThumbSize { get; set; } = 100;

    public string RenderMode { get; set; } = "Auto";
    public bool Prerender { get; set; } = true;
    public bool ShowPageInfo { get; set; } = false;

    public bool WsiViewerActive { get; set; }

    public string MaxFileSize { get; set; } = "10 MB";

    public long MaxFileSizeBytes => (long)(ByteSize.TryParse(MaxFileSize, out var size) ? size.Bytes : 10.Megabytes().Bytes);


    public string? ExternalStorageName { get; set; }
    public HashSet<string> WsiExtensions { get; set; } = [];

    public bool SyncImportEnabled { get; set; }
    public bool AiEnabled { get; set; }
    public bool WsiConversionEnabled { get; set; }
}
