using System.IO.Compression;
using FluentAssertions;
using iPath.Application.Contracts;
using iPath.Application.Features.Conversion;
using iPath.Application.Features.Conversion.Dzi;
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
            var stored = Path.Combine(root, docId.ToString());
            using (var zip = File.OpenRead(stored))
            {
                var index = DziZipIndexer.Build(zip);
                index.Success.Should().BeTrue(index.Error);
                index.Index!.TryGetTile(5, 1, 0, out _).Should().BeTrue();
            }
            document.File.Filename.Should().Be("Image202410181019_BADA_1230-A_01.dzi");
            document.File.ImageWidth.Should().Be(2000);
            document.File.ImageHeight.Should().Be(1000);
            Directory.GetFileSystemEntries(root).Should().BeEquivalentTo(
                [staging, stored], "nothing is extracted into the temp folder");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ProcessAsync_StoredZip_IsKeptByteForByte()
    {
        var root = NewTempDir();
        try
        {
            var (plugin, ctx, zipPath) = Arrange(root, CompressionLevel.NoCompression,
                ("slide.dzi", Descriptor),
                ("slide_files/8/0_0.webp", "preview"),
                ("slide_files/9/0_0.webp", "a"),
                ("slide_files/9/1_0.webp", "b"));

            var result = await plugin.ProcessAsync(ctx, CancellationToken.None);

            result.Success.Should().BeTrue(result.ErrorMessage);
            File.ReadAllBytes(Path.Combine(root, ctx.DocumentId.ToString()))
                .Should().Equal(File.ReadAllBytes(zipPath));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ProcessAsync_SetsPreviewWithoutOverwritingSlideDimensions()
    {
        var root = NewTempDir();
        try
        {
            var (plugin, ctx, _) = Arrange(root, CompressionLevel.NoCompression,
                ("slide.dzi", Descriptor),
                ("slide_files/8/0_0.webp", "preview"),
                ("slide_files/9/0_0.webp", "a"),
                ("slide_files/9/1_0.webp", "b"));

            await plugin.ProcessAsync(ctx, CancellationToken.None);

            ctx.Document.File.ThumbData.Should().Be(Convert.ToBase64String("preview"u8.ToArray()));
            ctx.Document.File.ImageWidth.Should().Be(4000);
            ctx.Document.File.ImageHeight.Should().Be(3000);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ProcessAsync_InvalidLayout_Fails()
    {
        var root = NewTempDir();
        try
        {
            var (plugin, ctx, _) = Arrange(root, CompressionLevel.NoCompression, ("readme.txt", "no slide here"));

            var result = await plugin.ProcessAsync(ctx, CancellationToken.None);

            result.Success.Should().BeFalse();
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task CreateThumbnailAsync_ReadsPreviewFromStoredZip()
    {
        var root = NewTempDir();
        try
        {
            var (plugin, ctx, zipPath) = Arrange(root, CompressionLevel.NoCompression,
                ("slide.dzi", Descriptor),
                ("slide_files/7/0_0.webp", "preview"));
            var thumb = new ThumbnailContext(ctx.DocumentId, zipPath, root, 100, ctx.Document);

            var result = await plugin.CreateThumbnailAsync(thumb, CancellationToken.None);

            result.Success.Should().BeTrue(result.ErrorMessage);
            ctx.Document.File.ThumbData.Should().Be(Convert.ToBase64String("preview"u8.ToArray()));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private const string Descriptor = """<?xml version="1.0" encoding="UTF-8"?><Image TileSize="254" Overlap="1" Format="webp" xmlns="http://schemas.microsoft.com/deepzoom/2008"><Size Height="3000" Width="4000"/></Image>""";

    private static (DziImportPlugin Plugin, ConversionJobContext Ctx, string ZipPath) Arrange(
        string root, CompressionLevel level, params (string Name, string Content)[] entries)
    {
        var docId = Guid.NewGuid();
        var staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(staging);
        var zipPath = Path.Combine(staging, "slide.dzi.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
                WriteEntry(archive, name, content, level);
        }

        var document = new DocumentNode
        {
            Id = docId,
            File = new NodeFile { Filename = "slide.dzi.zip", MimeType = "application/zip", ImageWidth = 100, ImageHeight = 100 }
        };
        var plugin = new DziImportPlugin(
            Options.Create(new iPathConfig { TempDataPath = root }),
            Substitute.For<ILogger<DziImportPlugin>>());
        var ctx = new ConversionJobContext(docId, staging, "slide.dzi.zip", ".zip", document);
        return (plugin, ctx, zipPath);
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content,
        CompressionLevel level = CompressionLevel.Optimal)
    {
        var entry = archive.CreateEntry(entryName, level);
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
