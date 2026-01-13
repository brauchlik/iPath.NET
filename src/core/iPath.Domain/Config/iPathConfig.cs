namespace iPath.Domain.Config;

public class iPathConfig
{
    public const string ConfigName = "iPathConfig";

    public bool DbSeedingActive { get; set; }
    public bool DbAutoMigrate { get; set; }

    public string DataRoot { get; set; } = string.Empty;
    public string TempDataPath { get; set; } = string.Empty;
    public string LocalDataPath { get; set; } = string.Empty;

    public bool ExportNodeJson { get; set; }

    public int ThumbSize { get; set; } = 100;

    public string RenderMode { get; set; } = "Auto";
    public bool Prerender { get; set; } = true;
    public bool ShowPageInfo { get; set; } = false;

    public string ReverseProxyAddresse { get; set; }
}
