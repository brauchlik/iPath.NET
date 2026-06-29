using System.Diagnostics;
using System.Text.RegularExpressions;
using VsiConverter.UI.Models;

namespace VsiConverter.UI.Services;

public static partial class SeriesDetector
{
    private static readonly Regex SeriesHeaderRx = SeriesHeaderRegex();
    private static readonly Regex DimensionRx = DimensionRegex();

    public static async Task<List<AvailableSeries>> DetectSeriesAsync(string vsiPath, CancellationToken ct = default)
    {
        var results = new List<AvailableSeries>();

        var settings = SettingsStore.Load();
        var bfconvertPath = settings.BfconvertPath ?? ToolchainManager.FindTool("bfconvert");
        if (bfconvertPath is null)
            return results;

        var jarPath = ResolveJar(bfconvertPath);
        if (jarPath is null)
            return results;

        var psi = new ProcessStartInfo("java", $"-cp \"{jarPath}\" loci.formats.tools.ImageInfo -nopix -no-upgrade \"{vsiPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null) return results;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cts.Token);
            var output = await outputTask;

            if (process.ExitCode != 0)
                return results;

            string? currentSeries = null;
            int? currentWidth = null;
            int? currentHeight = null;
            double? pixelSize = null;

            foreach (var line in output.Split('\n', '\r'))
            {
                var seriesMatch = SeriesHeaderRx.Match(line);
                if (seriesMatch.Success)
                {
                    if (currentSeries is not null && currentWidth.HasValue && currentHeight.HasValue)
                    {
                        results.Add(new AvailableSeries(
                            results.Count,
                            currentWidth.Value,
                            currentHeight.Value,
                            pixelSize ?? 0,
                            currentSeries));
                    }
                    currentSeries = seriesMatch.Groups[1].Value;
                    currentWidth = null;
                    currentHeight = null;
                    pixelSize = null;
                    continue;
                }

                var dimMatch = DimensionRx.Match(line);
                if (dimMatch.Success)
                {
                    var label = dimMatch.Groups[1].Value;
                    var value = int.Parse(dimMatch.Groups[2].Value);
                    if (label.Contains("Width")) currentWidth = value;
                    else if (label.Contains("Height")) currentHeight = value;
                }

                if (line.Contains("PixelSizeX") || line.Contains("pixelSizeX"))
                {
                    var parts = line.Split('=', ':');
                    if (parts.Length >= 2 && double.TryParse(parts[^1].Trim(), out var ps))
                        pixelSize = ps;
                }
            }

            if (currentSeries is not null && currentWidth.HasValue && currentHeight.HasValue)
            {
                results.Add(new AvailableSeries(
                    results.Count,
                    currentWidth.Value,
                    currentHeight.Value,
                    pixelSize ?? 0,
                    currentSeries));
            }
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
        }

        return results;
    }

    private static string? ResolveJar(string? bfconvertPath)
    {
        if (bfconvertPath is null) return null;
        var ext = Path.GetExtension(bfconvertPath);
        if (string.Equals(ext, ".jar", StringComparison.OrdinalIgnoreCase))
            return bfconvertPath;

        var dir = Path.GetDirectoryName(bfconvertPath)!;
        var jarPath = Path.Combine(dir, "bfconvert.jar");
        if (File.Exists(jarPath)) return jarPath;

        jarPath = Path.Combine(dir, "bioformats_package.jar");
        if (File.Exists(jarPath)) return jarPath;

        return null;
    }

    [GeneratedRegex(@"^\s*(?:Series|Pixels)\s+#?(\d+)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex SeriesHeaderRegex();

    [GeneratedRegex(@"^\s*(Width|Height)\s*=\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DimensionRegex();
}
