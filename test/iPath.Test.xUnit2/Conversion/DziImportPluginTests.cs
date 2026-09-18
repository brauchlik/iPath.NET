using System.IO.Compression;
using FluentAssertions;
using iPath.Application.Features.Conversion;
using iPath.Domain.Config;
using iPath.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace iPath.Test.xUnit2.Conversion;

public class ZipExtractionTests
{
    [Theory]
    [InlineData(@"foo_files\0\0.webp", "foo_files/0/0.webp")]
    [InlineData("foo_files/0/0.webp", "foo_files/0/0.webp")]
    [InlineData(@"Image-1_files\5\0_0.webp", "Image-1_files/5/0_0.webp")]
    public void NormalizeEntryPath_ConvertsBackslashesToForwardSlashes(string input, string expected)
    {
        ZipExtraction.NormalizeEntryPath(input).Should().Be(expected);
    }

    [Fact]
    public void ExtractNormalized_CreatesNestedFolder_FromBackslashEntries()
    {
        var root = NewTempDir();
        try
        {
            var zipPath = Path.Combine(root, "backslash.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("foo.dzi");
                archive.CreateEntry(@"foo_files\0\0.webp");
            }

            var target = Path.Combine(root, "out");
            ZipExtraction.ExtractNormalized(zipPath, target);

            File.Exists(Path.Combine(target, "foo.dzi")).Should().BeTrue();
            Directory.Exists(Path.Combine(target, "foo_files", "0")).Should().BeTrue();
            File.Exists(Path.Combine(target, "foo_files", "0", "0.webp")).Should().BeTrue();
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ipath-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, true); } catch { }
    }
}

public class DziImportPluginProcessTests
{
    [Fact]
    public async Task ProcessAsync_ImportsZipWithBackslashSeparatedTiles()
    {
        // Arrange
        var root = NewTempDir();
        try
        {
            var docId = Guid.NewGuid();
            var staging = Path.Combine(root, "staging");
            Directory.CreateDirectory(staging);

            var originalName = "Image202410181019_BADA_1230-A_01.dzi.zip";
            var zipPath = Path.Combine(staging, originalName);
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "Image202410181019_BADA_1230-A_01.dzi",
                    "<Image TileSize=\"254\" Overlap=\"1\" Format=\"webp\"><Size Height=\"1000\" Width=\"2000\"/></Image>");
                WriteEntry(archive, @"Image202410181019_BADA_1230-A_01_files\5\0_0.webp", "tile");
                WriteEntry(archive, @"Image202410181019_BADA_1230-A_01_files\5\1_0.webp", "tile");
            }

            var document = new DocumentNode
            {
                Id = docId,
                File = new NodeFile { Filename = originalName, MimeType = "application/zip" }
            };

            var plugin = new DziImportPlugin(
                Options.Create(new iPathConfig { TempDataPath = root }),
                Substitute.For<ILogger<DziImportPlugin>>());

            var ctx = new ConversionJobContext(
                DocumentId: docId,
                StagingPath: staging,
                OriginalFilename: originalName,
                FileExtension: ".zip",
                Document: document);

            // Act
            var result = await plugin.ProcessAsync(ctx, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue(result.ErrorMessage);
            File.Exists(Path.Combine(root, $"{docId}.dzi")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, $"{docId}_files")).Should().BeTrue();
            File.Exists(Path.Combine(root, $"{docId}_files", "5", "0_0.webp")).Should().BeTrue();
            document.File.Filename.Should().Be("Image202410181019_BADA_1230-A_01.dzi");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ipath-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, true); } catch { }
    }
}
