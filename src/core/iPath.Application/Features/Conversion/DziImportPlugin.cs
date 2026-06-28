using System.IO.Compression;
using iPath.Application.Contracts;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.Application.Features.Conversion;

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

        // Must contain exactly one .dzi file
        if (dziEntries.Count != 1)
            return false;

        var entry = dziEntries[0];

        // Count depth: split FullName by '/' and '\'
        var parts = entry.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        // Depth must be 0 (root level) or 1 (inside a single top-level folder)
        return parts.Length <= 2;
    }

    public bool RequiresConversion => false;

    public IReadOnlyList<string> GetRequiredCompanions(string fileName) => [];

    public async Task<ConversionResult> ProcessAsync(ConversionJobContext ctx, CancellationToken ct)
    {
        var inputPath = Path.Combine(ctx.StagingPath, ctx.OriginalFilename);
        var tempPath = ipathConfig.Value.TempDataPath;
        var extractTempDir = Path.Combine(tempPath, $"import-{ctx.DocumentId}");

        ctx.Document.File.ConversionStatus = DocumentConversionStatus.Converting;

        try
        {
            // 1. Extract the uploaded zip file to a temporary subdirectory
            if (Directory.Exists(extractTempDir))
                Directory.Delete(extractTempDir, true);
            Directory.CreateDirectory(extractTempDir);

            logger.LogInformation("Extracting pre-converted DZI zip {Path} to temp extraction dir {Temp}", inputPath, extractTempDir);
            ZipFile.ExtractToDirectory(inputPath, extractTempDir, overwriteFiles: true);

            // 2. Find the .dzi file inside the extracted structure
            var dziFiles = Directory.GetFiles(extractTempDir, "*.dzi", SearchOption.AllDirectories)
                .Where(f => !f.Contains("__MACOSX"))
                .ToList();

            if (dziFiles.Count == 0)
            {
                throw new FileNotFoundException("No .dzi file found in the zip archive.");
            }

            var sourceDziPath = dziFiles[0];
            var sourceDziDir = Path.GetDirectoryName(sourceDziPath)!;
            var baseName = Path.GetFileNameWithoutExtension(sourceDziPath);
            var sourceFilesDir = Path.Combine(sourceDziDir, $"{baseName}_files");

            if (!Directory.Exists(sourceFilesDir))
            {
                throw new DirectoryNotFoundException($"DZI files folder '{baseName}_files' not found in the zip archive.");
            }

            // 3. Move/Rename the .dzi file and its _files folder to the final cache path
            var finalDziPath = Path.Combine(tempPath, $"{ctx.DocumentId}.dzi");
            var finalFilesPath = Path.Combine(tempPath, $"{ctx.DocumentId}_files");
            var canonicalZipPath = Path.Combine(tempPath, $"{ctx.DocumentId}.zip");

            if (File.Exists(finalDziPath)) File.Delete(finalDziPath);
            if (Directory.Exists(finalFilesPath)) Directory.Delete(finalFilesPath, true);

            File.Move(sourceDziPath, finalDziPath);
            Directory.Move(sourceFilesDir, finalFilesPath);

            // 4. Re-zip with canonical {id}.dzi + {id}_files/ entries for consistent cache-miss restoration
            if (File.Exists(canonicalZipPath)) File.Delete(canonicalZipPath);
            using (var archive = ZipFile.Open(canonicalZipPath, ZipArchiveMode.Create))
            {
                var docIdStr = ctx.DocumentId.ToString();
                var dziEntryName = $"{docIdStr}.dzi";
                var filesEntryPrefix = $"{docIdStr}_files";
                archive.CreateEntryFromFile(finalDziPath, dziEntryName, CompressionLevel.NoCompression);
                AddDirectoryToArchive(archive, finalFilesPath, filesEntryPrefix, CompressionLevel.NoCompression);
            }

            var origFilePath = Path.Combine(tempPath, ctx.DocumentId.ToString());
            if (File.Exists(origFilePath)) File.Delete(origFilePath);
            File.Move(canonicalZipPath, origFilePath);

            // Parse DZI metadata for dimensions
            try
            {
                ParseDziDimensions(finalDziPath, ctx);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse DZI metadata for dimensions");
            }

            // 5. Clean up the temporary extraction directory
            try
            {
                Directory.Delete(extractTempDir, true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to clean up temporary DZI extraction directory {Dir}", extractTempDir);
            }

            // 6. Extract thumbnail
            var thumbContext = new ThumbnailContext(ctx.DocumentId, canonicalZipPath, tempPath, 100, ctx.Document);
            await CreateThumbnailAsync(thumbContext, ct);

            ctx.Document.File.Filename = Path.ChangeExtension(ctx.Document.File.Filename, ".dzi");
            ctx.Document.File.ConversionStatus = DocumentConversionStatus.Completed;
            return ConversionResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import zipped DZI for document {DocId}", ctx.DocumentId);
            try
            {
                if (Directory.Exists(extractTempDir))
                    Directory.Delete(extractTempDir, true);
            }
            catch { }
            return ConversionResult.Fail($"Failed to import DZI: {ex.Message}");
        }
    }

    private void AddDirectoryToArchive(ZipArchive archive, string sourceDir, string archivePath, CompressionLevel level)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var entryName = Path.Combine(archivePath, Path.GetFileName(file)).Replace('\\', '/');
            archive.CreateEntryFromFile(file, entryName, level);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var entryName = Path.Combine(archivePath, Path.GetFileName(dir)).Replace('\\', '/');
            AddDirectoryToArchive(archive, dir, entryName, level);
        }
    }

    private static void ParseDziDimensions(string dziPath, ConversionJobContext ctx)
    {
        var xml = File.ReadAllText(dziPath);
        var match = System.Text.RegularExpressions.Regex.Match(xml,
            @"<Size\s+Height=""(\d+)""\s+Width=""(\d+)""");
        if (match.Success)
        {
            ctx.Document.File.ImageWidth = int.Parse(match.Groups[2].Value);
            ctx.Document.File.ImageHeight = int.Parse(match.Groups[1].Value);
        }
    }

    public async Task<ThumbnailResult> CreateThumbnailAsync(ThumbnailContext ctx, CancellationToken ct)
    {
        var id = ctx.DocumentId.ToString();
        var dziDir = Path.Combine(ctx.TempDataPath, $"{id}_files");

        if (!Directory.Exists(dziDir))
        {
            logger.LogWarning("DZI files directory not found at {Dir} for thumbnail creation.", dziDir);
            return ThumbnailResult.Fail("DZI files directory not found");
        }

        try
        {
            // Find a suitable single tile to use as thumbnail
            // Walk down from level 12 to 0 to find the first level that has exactly one tile
            for (int level = 12; level >= 0; level--)
            {
                var levelDir = Path.Combine(dziDir, level.ToString());
                if (Directory.Exists(levelDir))
                {
                    var files = Directory.GetFiles(levelDir);
                    if (files.Length == 1)
                    {
                        var file = files[0];
                        var bytes = await File.ReadAllBytesAsync(file, ct);
                        ctx.Document.File.ThumbData = Convert.ToBase64String(bytes);
                        ctx.Document.File.ImageWidth = ctx.ThumbSize;
                        ctx.Document.File.ImageHeight = ctx.ThumbSize;
                        
                        logger.LogInformation("DziImportPlugin: Generated thumbnail from single tile at level {Level}", level);
                        return ThumbnailResult.Ok();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create thumbnail from DZI tiles for {DocId}", ctx.DocumentId);
            return ThumbnailResult.Fail(ex.Message);
        }

        return ThumbnailResult.Fail("No single-tile level found for thumbnail");
    }
}
