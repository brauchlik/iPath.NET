using Humanizer;

namespace iPath.Domain.Config;

public class iPathConfig
{
    public const string ConfigName = "iPathConfig";

    public bool DbSeedingActive { get; set; }
    public bool DbAutoMigrate { get; set; }

    public string DataRoot { get; set; } = string.Empty;
    public string TempDataPath { get; set; } = string.Empty;
    public string LocalDataPath { get; set; } = string.Empty;
    public string FhirResourceFilePath { get; set; } = string.Empty;

    public bool ExportNodeJson { get; set; }

    public string ReverseProxyAddresse { get; set; }
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

}
