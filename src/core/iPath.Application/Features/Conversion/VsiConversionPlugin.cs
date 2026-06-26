using System.Diagnostics;
using iPath.Application.Contracts;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.Application.Features.Conversion;

public class VsiConversionPlugin(
    IOptions<VsiConversionConfig> config,
    IOptions<iPathConfig> ipathConfig,
    ILogger<VsiConversionPlugin> logger)
    : IConversionPlugin
{
    public bool CanHandle(string extension) =>
        string.Equals(extension, ".vsi", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetRequiredCompanions(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        return [$"_{baseName}_"];
    }

    public async Task<ConversionResult> ProcessAsync(ConversionJobContext ctx, CancellationToken ct)
    {
        var cfg = config.Value;
        var inputPath = Path.Combine(ctx.StagingPath, ctx.OriginalFilename);
        var tiffPath = Path.Combine(ctx.StagingPath, $"{ctx.DocumentId}.ome.tiff");
        var dziOutput = Path.Combine(ipathConfig.Value.TempDataPath, ctx.DocumentId.ToString());

        // Step 1: bfconvert .vsi → OME-TIFF
        logger.LogInformation("bfconvert: {Series} {Input} -> {Output}",
            cfg.SeriesIndex, inputPath, tiffPath);
        var bfResult = await RunProcessAsync(
            cfg.BfconvertPath,
            $"-series {cfg.SeriesIndex} -compression JPEG \"{inputPath}\" \"{tiffPath}\"",
            cfg.JavaMaxMemory, cfg.MaxConversionMinutes, ct);
        if (bfResult != null) return ConversionResult.Fail(bfResult);

        var tiffInfo = new FileInfo(tiffPath);
        if (!tiffInfo.Exists || tiffInfo.Length == 0)
            return ConversionResult.Fail("bfconvert produced no output file");

        logger.LogInformation("OME-TIFF: {SizeMB} MB", tiffInfo.Length / (1024 * 1024));

        // Step 2: vips dzsave → DZI tiles
        logger.LogInformation("vips dzsave: {Input} -> {Output}", tiffPath, dziOutput);
        var vipsResult = await RunProcessAsync(
            cfg.VipsPath,
            $"dzsave \"{tiffPath}\" \"{dziOutput}\" --tile-size 254 --overlap 1 --suffix .webp[Q={cfg.WebpQuality}]",
            null, cfg.MaxConversionMinutes, ct);
        if (vipsResult != null) return ConversionResult.Fail(vipsResult);

        // Step 3: Extract thumbnail from DZI level 0
        await ExtractThumbnailAsync(dziOutput, ctx);

        // Step 4: Cleanup intermediate TIFF
        try { File.Delete(tiffPath); } catch { }

        return ConversionResult.Ok();
    }

    private async Task ExtractThumbnailAsync(string dziOutput, ConversionJobContext ctx)
    {
        var thumbPath = Path.Combine($"{dziOutput}_files", "0", "0_0.webp");
        if (File.Exists(thumbPath))
        {
            var bytes = await File.ReadAllBytesAsync(thumbPath);
            ctx.Document.File.ThumbData = Convert.ToBase64String(bytes);
        }
    }

    private async Task<string?> RunProcessAsync(
        string fileName, string arguments, string? envMemory,
        int maxMinutes, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(envMemory))
            psi.Environment["BF_MAX_MEM"] = envMemory;

        using var proc = new Process { StartInfo = psi };

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                logger.LogDebug("bfconvert: {Data}", e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                logger.LogWarning("process stderr: {Data}", e.Data);
        };

        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) => tcs.TrySetResult(true);

        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromMinutes(maxMinutes));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linked.Token));

            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
                return $"Process timed out after {maxMinutes} minutes";
            }

            if (proc.ExitCode != 0)
                return $"Process exited with code {proc.ExitCode}";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (!proc.HasExited)
                proc.Kill(entireProcessTree: true);
            return $"Process timed out after {maxMinutes} minutes";
        }

        return null; // success
    }
}
