using System.Diagnostics;
using System.IO.Compression;
using iPath.Application.Contracts;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.Application.Features.Conversion;

public class WsiConversionPlugin(
    IOptions<WsiConversionConfig> config,
    IOptions<iPathConfig> ipathConfig,
    ILogger<WsiConversionPlugin> logger)
    : IConversionPlugin
{
    private static readonly HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".svs", ".ndpi", ".tiff"
    };

    public bool CanHandle(string extension) => _extensions.Contains(extension);

    public bool CanHandleZip(ZipArchive archive) => false;

    public bool RequiresConversion => true;

    public IReadOnlyList<string> GetRequiredCompanions(string fileName) => [];

    public async Task<ConversionResult> ProcessAsync(ConversionJobContext ctx, CancellationToken ct)
    {
        ctx.Document.File.ConversionStatus = DocumentConversionStatus.Converting;

        var sourcePath = Path.Combine(ctx.StagingPath, ctx.OriginalFilename);
        var needsDzi = config.Value.ConvertToDzi.TryGetValue(ctx.FileExtension, out var convert) && convert;

        if (needsDzi)
        {
            var tiffPath = Path.Combine(ctx.StagingPath, $"{ctx.DocumentId}.ome.tiff");
            var dziOutput = Path.Combine(ipathConfig.Value.TempDataPath, ctx.DocumentId.ToString());

            logger.LogInformation("bfconvert: {Series} {Input} -> {Output}",
                config.Value.SeriesIndex, sourcePath, tiffPath);
            var bfResult = await RunProcessAsync(
                config.Value.BfconvertPath,
                $"-series {config.Value.SeriesIndex} -compression JPEG \"{sourcePath}\" \"{tiffPath}\"",
                config.Value.JavaMaxMemory, config.Value.MaxConversionMinutes, ct);
            if (bfResult != null) return ConversionResult.Fail(bfResult);

            logger.LogInformation("vips dzsave: {Input} -> {Output}", tiffPath, dziOutput);
            var vipsResult = await RunProcessAsync(
                config.Value.VipsPath,
                $"dzsave \"{tiffPath}\" \"{dziOutput}\" --tile-size 254 --overlap 1 --suffix .webp[Q={config.Value.WebpQuality}]",
                null, config.Value.MaxConversionMinutes, ct);
            if (vipsResult != null) return ConversionResult.Fail(vipsResult);

            try { File.Delete(tiffPath); } catch { }
        }

        // Extract thumbnail
        await CreateThumbnailAsync(
            new ThumbnailContext(ctx.DocumentId, sourcePath, ipathConfig.Value.TempDataPath, 100, ctx.Document), ct);

        ctx.Document.File.ConversionStatus = DocumentConversionStatus.Completed;
        return ConversionResult.Ok();
    }

    public async Task<ThumbnailResult> CreateThumbnailAsync(ThumbnailContext ctx, CancellationToken ct)
    {
        // Try direct vips thumbnail on source (works for .tiff, .svs)
        var thumbOutput = Path.GetTempFileName() + ".jpg";
        try
        {
            var result = await RunProcessAsync(
                config.Value.VipsPath,
                $"thumbnail \"{ctx.SourcePath}\" \"{thumbOutput}\" {ctx.ThumbSize}",
                null, 2, ct);
            if (result == null && File.Exists(thumbOutput))
            {
                var bytes = await File.ReadAllBytesAsync(thumbOutput, ct);
                ctx.Document.File.ThumbData = Convert.ToBase64String(bytes);
                ctx.Document.File.ImageWidth = ctx.ThumbSize;
                ctx.Document.File.ImageHeight = ctx.ThumbSize;
                return ThumbnailResult.Ok();
            }
        }
        finally { try { File.Delete(thumbOutput); } catch { } }

        // Fallback: bfconvert to get an overview, then vips thumbnail
        var overviewPath = Path.GetTempFileName() + ".ome.tiff";
        try
        {
            var result = await RunProcessAsync(
                config.Value.BfconvertPath,
                $"-series {config.Value.SeriesIndex} -compression JPEG \"{ctx.SourcePath}\" \"{overviewPath}\"",
                config.Value.JavaMaxMemory, 2, ct);
            if (result == null && File.Exists(overviewPath))
            {
                await VipsThumbnailFromAsync(overviewPath, ctx, ct);
                return ThumbnailResult.Ok();
            }
        }
        finally { try { File.Delete(overviewPath); } catch { } }

        return ThumbnailResult.Fail("No source available for thumbnail");
    }

    private async Task VipsThumbnailFromAsync(string inputPath, ThumbnailContext ctx, CancellationToken ct)
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
                var bytes = await File.ReadAllBytesAsync(thumbOutput, ct);
                ctx.Document.File.ThumbData = Convert.ToBase64String(bytes);
                ctx.Document.File.ImageWidth = ctx.ThumbSize;
                ctx.Document.File.ImageHeight = ctx.ThumbSize;
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
