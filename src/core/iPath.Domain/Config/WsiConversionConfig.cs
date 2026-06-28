namespace iPath.Domain.Config;

public class WsiConversionConfig
{
    public const string ConfigName = "WsiConversion";

    public bool Enabled { get; set; }
    public string BfconvertPath { get; set; } = "bfconvert";
    public string JavaMaxMemory { get; set; } = "8g";
    public int MaxConversionMinutes { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public string? TempPath { get; set; }
    public int SeriesIndex { get; set; } = 7;
    public string VipsPath { get; set; } = "vips";
    public int WebpQuality { get; set; } = 80;
    public string StagingPath { get; set; } = "";
    public Dictionary<string, bool> ConvertToDzi { get; set; } = new()
    {
        [".ndpi"] = true,
        [".svs"] = false,
        [".tiff"] = false
    };
}

public class GDriveImportScannerConfig
{
    public const string ConfigName = "GDriveImportScanner";

    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 5;
}
