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
        // Option 1: vips thumbnail directly on source file (fastest, best quality)
        if (File.Exists(ctx.SourcePath))
        {
            await VipsThumbnailAsync(ctx.SourcePath, ctx, ct);
            return ThumbnailResult.Ok();
        }

        // Option 2: DZI fallback — stitch tiles with vips join, then thumbnail
        var dziDir = Path.Combine(ctx.TempDataPath, $"{ctx.DocumentId}_files");
        logger.LogInformation("DZI fallback: looking for tiles in {Dir}", dziDir);

        for (int level = 0; level <= 15; level++)
        {
            var levelDir = Path.Combine(dziDir, $"{level}");
            if (!Directory.Exists(levelDir)) continue;

            var files = Directory.GetFiles(levelDir, "*.webp");
            if (files.Length < 2 || files.Length > 9) continue;

            logger.LogInformation("DZI fallback: level {Level} has {Count} tiles", level, files.Length);

            try
            {
                var composite = await StitchTilesAsync(files, ct);
                if (composite != null)
                {
                    await VipsThumbnailAsync(composite, ctx, ct);
                    try { File.Delete(composite); } catch { }
                    return ThumbnailResult.Ok();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DZI stitch failed for level {Level}", level);
            }
        }

        logger.LogWarning("DZI fallback failed: no stitchable level found in {Dir}", dziDir);
        return ThumbnailResult.Fail("Source file not available for thumbnail");
    }

    private async Task<string?> StitchTilesAsync(string[] tiles, CancellationToken ct)
    {
        var vips = config.Value.VipsPath;

        var parsed = tiles.Select(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var parts = name.Split('_');
            return (Col: int.Parse(parts[0]), Row: int.Parse(parts[1]), Path: f);
        }).ToList();

        // Group by row, stitch each row horizontally
        var rows = parsed.GroupBy(t => t.Row).OrderBy(g => g.Key);
        var rowComposites = new List<string>();

        foreach (var row in rows)
        {
            var rowTiles = row.OrderBy(t => t.Col).ToList();
            var current = rowTiles[0].Path;
            logger.LogDebug("Stitching row {Row}: {Count} tiles", row.Key, rowTiles.Count);

            for (int i = 1; i < rowTiles.Count; i++)
            {
                var output = Path.GetTempFileName() + ".v";
                logger.LogDebug("  join {Left} + {Right} -> {Out}", current, rowTiles[i].Path, output);
                var result = await RunProcessAsync(vips,
                    $"join \"{current}\" \"{rowTiles[i].Path}\" \"{output}\" horizontal", null, 1, ct);
                if (result != null) { logger.LogWarning("hjoin failed: {Error}", result); return null; }
                if (!File.Exists(output)) { logger.LogWarning("hjoin produced no output"); return null; }
                if (current != rowTiles[0].Path) try { File.Delete(current); } catch { }
                current = output;
            }
            rowComposites.Add(current);
        }

        // Stitch rows vertically
        var composite = rowComposites[0];
        for (int i = 1; i < rowComposites.Count; i++)
        {
            var output = Path.GetTempFileName() + ".v";
            logger.LogDebug("vjoin {Top} + {Bottom} -> {Out}", composite, rowComposites[i], output);
            var result = await RunProcessAsync(vips,
                $"join \"{composite}\" \"{rowComposites[i]}\" \"{output}\" vertical", null, 1, ct);
            if (result != null) { logger.LogWarning("vjoin failed: {Error}", result); return null; }
            if (!File.Exists(output)) { logger.LogWarning("vjoin produced no output"); return null; }
            if (composite != rowComposites[0]) try { File.Delete(composite); } catch { }
            composite = output;
        }

        logger.LogInformation("DZI stitch complete: {Path}", composite);
        return composite;
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
