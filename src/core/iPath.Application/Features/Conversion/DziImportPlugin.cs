using System.IO.Compression;
using System.Xml.Linq;
using iPath.Application.Contracts;
using iPath.Application.Features.Conversion.Dzi;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.Application.Features.Conversion;

/// <summary>
/// Imports a pre-converted DZI zip (e.g. from the VsiConverter tool). The zip is stored as
/// uploaded and served by range reads through its <see cref="DziTileIndex"/>; it is only
/// repacked when its entries are compressed.
/// </summary>
public class DziImportPlugin(
    IOptions<iPathConfig> ipathConfig,
    ILogger<DziImportPlugin> logger)
    : IConversionPlugin
{
    public bool CanHandle(string extension) =>
        string.Equals(extension, ".dzi", StringComparison.OrdinalIgnoreCase);

    public bool CanHandleZip(ZipArchive archive)
    {
        var dziEntries = archive.Entries
            .Where(e => e.Name.EndsWith(".dzi", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains("__MACOSX"))
            .ToList();

        if (dziEntries.Count != 1)
            return false;

        // Root level or inside a single top-level folder.
        var parts = dziEntries[0].FullName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 2;
    }

    public bool RequiresConversion => false;

    public IReadOnlyList<string> GetRequiredCompanions(string fileName) => [];

    public async Task<ConversionResult> ProcessAsync(ConversionJobContext ctx, CancellationToken ct)
    {
        var inputPath = Path.Combine(ctx.StagingPath, ctx.OriginalFilename);
        var storedZipPath = Path.Combine(ipathConfig.Value.TempDataPath, ctx.DocumentId.ToString());

        ctx.Document.File.ConversionStatus = DocumentConversionStatus.Converting;

        try
        {
            var index = BuildIndex(inputPath, out var result);
            if (result.RequiresRepack)
            {
                logger.LogInformation("DZI zip {Path} has compressed entries, repacking uncompressed", inputPath);
                RepackStored(inputPath, storedZipPath);
                index = BuildIndex(storedZipPath, out result);
            }
            else if (index is not null)
            {
                CopyIfDifferent(inputPath, storedZipPath);
            }

            if (index is null)
                return ConversionResult.Fail($"Failed to import DZI: {result.Error}");

            // Stored next to the zip, so remote instances serve tiles without reading the zip's directory.
            await using (var sidecar = File.Create(storedZipPath + ".tileindex"))
            {
                index.WriteTo(sidecar);
            }

            await using (var zip = OpenRead(storedZipPath))
            {
                try
                {
                    ApplyDimensions(await ReadRangeAsync(zip, index.Descriptor, ct), ctx.Document);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse DZI descriptor for dimensions of document {DocId}", ctx.DocumentId);
                }

                await ApplyPreviewAsync(zip, index, ctx.Document, ct);
            }

            logger.LogInformation("Imported DZI zip for document {DocId}: {Tiles} tiles", ctx.DocumentId, index.TileCount);

            ctx.Document.File.Filename = ToDziFileName(ctx.Document.File.Filename);
            ctx.Document.File.ConversionStatus = DocumentConversionStatus.Completed;
            return ConversionResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import zipped DZI for document {DocId}", ctx.DocumentId);
            return ConversionResult.Fail($"Failed to import DZI: {ex.Message}");
        }
    }

    public async Task<ThumbnailResult> CreateThumbnailAsync(ThumbnailContext ctx, CancellationToken ct)
    {
        if (!File.Exists(ctx.SourcePath))
            return ThumbnailResult.Fail($"DZI zip not found at {ctx.SourcePath}");

        try
        {
            var index = BuildIndex(ctx.SourcePath, out var result);
            if (index is null)
                return ThumbnailResult.Fail(result.Error ?? "Not an indexable DZI zip");

            await using var zip = OpenRead(ctx.SourcePath);
            return await ApplyPreviewAsync(zip, index, ctx.Document, ct)
                ? ThumbnailResult.Ok()
                : ThumbnailResult.Fail("No single-tile level found for thumbnail");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create thumbnail from DZI zip for {DocId}", ctx.DocumentId);
            return ThumbnailResult.Fail(ex.Message);
        }
    }

    private async Task<bool> ApplyPreviewAsync(Stream zip, DziTileIndex index, Domain.Entities.DocumentNode document, CancellationToken ct)
    {
        // The single-tile level is the whole slide at small size; its bytes are used as-is.
        // Slide dimensions come from the descriptor and are deliberately left untouched here.
        var preview = index.FindPreviewTile();
        if (preview is null)
            return false;

        document.File.ThumbData = Convert.ToBase64String(await ReadRangeAsync(zip, preview.Value, ct));
        return true;
    }

    private static DziTileIndex? BuildIndex(string zipPath, out DziZipIndexResult result)
    {
        using var zip = OpenRead(zipPath);
        result = DziZipIndexer.Build(zip);
        return result.Index;
    }

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.RandomAccess | FileOptions.Asynchronous);

    private static async Task<byte[]> ReadRangeAsync(Stream zip, ZipRange range, CancellationToken ct)
    {
        var buffer = new byte[range.Length];
        zip.Position = range.Offset;
        await zip.ReadExactlyAsync(buffer, ct);
        return buffer;
    }

    private static void ApplyDimensions(byte[] descriptor, Domain.Entities.DocumentNode document)
    {
        using var stream = new MemoryStream(descriptor);
        var size = XDocument.Load(stream).Descendants().FirstOrDefault(e => e.Name.LocalName == "Size");
        if (size is null)
            return;
        if (int.TryParse(size.Attribute("Width")?.Value, out var width))
            document.File.ImageWidth = width;
        if (int.TryParse(size.Attribute("Height")?.Value, out var height))
            document.File.ImageHeight = height;
    }

    private static void CopyIfDifferent(string source, string target)
    {
        // The upload handler already wrote the same bytes to the target; avoid a second multi-GB copy.
        if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(source).Length)
            return;
        File.Copy(source, target, overwrite: true);
    }

    private static void RepackStored(string sourceZipPath, string targetZipPath)
    {
        var temp = targetZipPath + ".repack";
        using (var source = ZipFile.OpenRead(sourceZipPath))
        using (var target = ZipFile.Open(temp, ZipArchiveMode.Create))
        {
            foreach (var entry in source.Entries)
            {
                var name = ZipExtraction.NormalizeEntryPath(entry.FullName);
                if (string.IsNullOrEmpty(name) || name.EndsWith('/'))
                    continue;
                var copy = target.CreateEntry(name, CompressionLevel.NoCompression);
                using var input = entry.Open();
                using var output = copy.Open();
                input.CopyTo(output);
            }
        }
        File.Move(temp, targetZipPath, overwrite: true);
    }

    private static string ToDziFileName(string? filename)
    {
        var name = string.IsNullOrWhiteSpace(filename) ? "slide" : filename;
        if (name.EndsWith(".dzi.zip", StringComparison.OrdinalIgnoreCase))
            return name[..^8] + ".dzi";
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return name[..^4] + ".dzi";
        return Path.ChangeExtension(name, ".dzi");
    }
}
