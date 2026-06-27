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

        ctx.Document.File.ConversionStatus = "converting";

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

        // Step 3: Extract thumbnail
        await CreateThumbnailAsync(
            new ThumbnailContext(ctx.DocumentId, inputPath, ipathConfig.Value.TempDataPath, 100, ctx.Document), ct);

        // Step 4: Cleanup intermediate TIFF
        try { File.Delete(tiffPath); } catch { }

        ctx.Document.File.ConversionStatus = "completed";
        return ConversionResult.Ok();
    }

    public async Task<ThumbnailResult> CreateThumbnailAsync(ThumbnailContext ctx, CancellationToken ct)
    {
        // Option 1: vips thumbnail directly on source (fastest, proven to work)
        if (File.Exists(ctx.SourcePath))
        {
            await VipsThumbnailAsync(ctx.SourcePath, ctx, ct);
            return ThumbnailResult.Ok();
        }

        // Option 2 (fallback): best DZI tile by file size (most content)
        var dziDir = Path.Combine(ctx.TempDataPath, $"{ctx.DocumentId}_files");
        if (Directory.Exists(dziDir))
        {
            long bestSize = 0;
            string? bestTile = null;
            for (int level = 0; level <= 15; level++)
            {
                var tile = Path.Combine(dziDir, $"{level}", "0_0.webp");
                if (File.Exists(tile))
                {
                    var fi = new FileInfo(tile);
                    if (fi.Length > bestSize) { bestSize = fi.Length; bestTile = tile; }
                }
            }
            if (bestTile != null)
            {
                await VipsThumbnailAsync(bestTile, ctx, ct);
                return ThumbnailResult.Ok();
            }
        }

        // Option 3 (last resort): bfconvert overview -> vips thumbnail
        if (File.Exists(ctx.SourcePath))
        {
            var overviewPath = Path.GetTempFileName() + ".ome.tiff";
            try
            {
                var result = await RunProcessAsync(
                    config.Value.BfconvertPath,
                    $"-series {config.Value.SeriesIndex} -compression JPEG \"{ctx.SourcePath}\" \"{overviewPath}\"",
                    config.Value.JavaMaxMemory, 2, ct);
                if (result == null && File.Exists(overviewPath))
                {
                    await VipsThumbnailAsync(overviewPath, ctx, ct);
                    return ThumbnailResult.Ok();
                }
            }
            finally { try { File.Delete(overviewPath); } catch { } }
        }

        return ThumbnailResult.Fail("No source available for thumbnail");
    }

    private async Task VipsThumbnailAsync(string inputPath, ThumbnailContext ctx, CancellationToken ct)
    {
        var thumbOutput = Path.GetTempFileName() + ".jpg";
        try
        {
            var result = await RunProcessAsync(
                config.Value.VipsPath,
                $"thumbnail \"{inputPath}\" \"{thumbOutput}\" {ctx.ThumbSize}",
                null, 1, ct);
            if (result == null && File.Exists(thumbOutput))
            {
                var fi = new FileInfo(thumbOutput);
                if (fi.Length > 200)
                {
                    var bytes = await File.ReadAllBytesAsync(thumbOutput, ct);
                    ctx.Document.File.ThumbData = Convert.ToBase64String(bytes);
                    ctx.Document.File.ImageWidth = ctx.ThumbSize;
                    ctx.Document.File.ImageHeight = ctx.ThumbSize;
                }
                else
                {
                    logger.LogWarning("vips thumbnail produced {Length} bytes for {Input}", fi.Length, inputPath);
                }
            }
            else if (result != null)
            {
                logger.LogWarning("vips thumbnail failed for {Input}: {Error}", inputPath, result);
            }
        }
        finally { try { File.Delete(thumbOutput); } catch { } }
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
                logger.LogDebug("process: {Data}", e.Data);
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
