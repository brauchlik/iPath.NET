namespace iPath.Domain.Config;

public static class WsiFormats
{
    /// <summary>TIFF-based slides the viewer reads directly (GeoTIFF tile source), without conversion.</summary>
    public static bool IsNativelyViewable(string? filename) =>
        Path.GetExtension(filename ?? "").ToLowerInvariant() is ".svs" or ".tif" or ".tiff";
}
