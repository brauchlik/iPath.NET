using System.Diagnostics;
using System.IO.Compression;

namespace VsiConverter.UI.Services;

public record ConversionResult(bool Success, string? OutputPath, string? ErrorMessage, bool IsCancelled = false);

public record ConversionProgress(string Stage, int Percent, string? Detail);

public class PipelineRunner
{
    private readonly string _bfconvertPath;
    private readonly string _vipsPath;

    public PipelineRunner()
    {
        var settings = SettingsStore.Load();
        _bfconvertPath = settings.BfconvertPath ?? ToolchainManager.FindTool("bfconvert") ?? "bfconvert";
        _vipsPath = settings.VipsPath ?? ToolchainManager.FindTool("vips") ?? "vips";
    }

    public async Task<ConversionResult> RunAsync(
        string vsiPath,
        int seriesIndex,
        int quality,
        IProgress<ConversionProgress> progress,
        CancellationToken ct)
    {
        var vsiDir = Path.GetDirectoryName(vsiPath)!;
        var baseName = Path.GetFileNameWithoutExtension(vsiPath);
        var tempDir = Path.Combine(Path.GetTempPath(), "VsiConverter", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Check companion folder
            progress.Report(new ConversionProgress("Checking companion", 0, null));
            var companionDir = Path.Combine(vsiDir, $"_{baseName}_");
            if (!Directory.Exists(companionDir))
            {
                return new ConversionResult(false, null,
                    $"Companion folder not found: expected '{companionDir}' next to the .vsi file.");
            }

            // bfconvert .vsi → OME-TIFF
            var omeTiff = Path.Combine(tempDir, $"{baseName}.ome.tiff");
            progress.Report(new ConversionProgress("Converting to OME-TIFF", 5, "bfconvert"));

            var bfArgs = $"-series {seriesIndex} -compression JPEG \"{vsiPath}\" \"{omeTiff}\"";

            bool bfResult;
            int bfPct = 5;
            Action<string> onBfLine = line =>
            {
                bfPct = Math.Min(bfPct + 1, 45);
                progress.Report(new ConversionProgress("Converting to OME-TIFF", bfPct, line));
            };
            if (_bfconvertPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                var javaPath = ToolchainManager.FindTool("java") ?? "java";
                var javaArgs = $"-cp \"{_bfconvertPath}\" loci.formats.tools.ImageConverter {bfArgs}";
                bfResult = await RunProcessAsync(javaPath, javaArgs, null, TimeSpan.FromMinutes(30), progress, ct, onBfLine, "bfconvert");
            }
            else
            {
                bfResult = await RunProcessAsync(
                    _bfconvertPath, bfArgs,
                    new Dictionary<string, string> { ["BF_MAX_MEM"] = "8g" },
                    TimeSpan.FromMinutes(30),
                    progress, ct, onBfLine, "bfconvert");
            }

            if (!bfResult)
            {
                return new ConversionResult(false, null, "bfconvert failed to convert .vsi to OME-TIFF.");
            }

            // vips dzsave → DZI tiles
            var dziBase = Path.Combine(tempDir, baseName);
            progress.Report(new ConversionProgress("Creating DZI tiles", 50, "vips dzsave"));

            var vipsArgs = $"dzsave \"{omeTiff}\" \"{dziBase}\" --tile-size 254 --overlap 1 --suffix \".webp[Q={quality}]\"";

            int vipsPct = 50;
            var vipsResult = await RunProcessAsync(
                _vipsPath, vipsArgs, null,
                TimeSpan.FromMinutes(30),
                progress,
                ct,
                line =>
                {
                    vipsPct = Math.Min(vipsPct + 1, 85);
                    progress.Report(new ConversionProgress("Creating DZI tiles", vipsPct, line));
                },
                "vips");

            if (!vipsResult)
            {
                return new ConversionResult(false, null, "vips dzsave failed to create DZI tiles.");
            }

            // Zip DZI output
            var outputZip = Path.Combine(vsiDir, $"{baseName}.dzi.zip");
            progress.Report(new ConversionProgress("Zipping DZI", 90, null));

            if (File.Exists(outputZip)) File.Delete(outputZip);

            using (var zip = ZipFile.Open(outputZip, ZipArchiveMode.Create))
            {
                var dziFile = dziBase + ".dzi";
                if (File.Exists(dziFile))
                {
                    zip.CreateEntryFromFile(dziFile, $"{baseName}.dzi", CompressionLevel.NoCompression);
                }

                var filesDir = dziBase + "_files";
                if (Directory.Exists(filesDir))
                {
                    foreach (var file in Directory.GetFiles(filesDir, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(tempDir, file);
                        zip.CreateEntryFromFile(file, relativePath, CompressionLevel.NoCompression);
                    }
                }
            }

            progress.Report(new ConversionProgress("Completed", 100, outputZip));
            return new ConversionResult(true, outputZip, null);
        }
        catch (OperationCanceledException)
        {
            return new ConversionResult(false, null, "Conversion was cancelled.", IsCancelled: true);
        }
        catch (Exception ex)
        {
            return new ConversionResult(false, null, $"Conversion failed: {ex.Message}");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static async Task<bool> RunProcessAsync(
        string fileName,
        string arguments,
        Dictionary<string, string>? environment,
        TimeSpan timeout,
        IProgress<ConversionProgress> progress,
        CancellationToken ct,
        Action<string>? onStderrLine = null,
        string stderrStage = "")
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (environment is not null)
        {
            foreach (var kvp in environment)
                psi.EnvironmentVariables[kvp.Key] = kvp.Value;
        }

        using var process = Process.Start(psi);
        if (process is null) return false;

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = onStderrLine is not null
            ? ReadStderrLinesAsync(process.StandardError, onStderrLine, stderrStage)
            : process.StandardError.ReadToEndAsync();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout, not user cancellation
            try { process.Kill(); } catch { }
            return false;
        }

        await Task.WhenAll(stdoutTask, stderrTask);

        return process.ExitCode == 0;
    }

    private static async Task ReadStderrLinesAsync(StreamReader reader, Action<string> onLine, string stage)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            Console.WriteLine($"[{stage}] {line}");
            if (!string.IsNullOrWhiteSpace(line))
                onLine(line);
        }
    }
}
