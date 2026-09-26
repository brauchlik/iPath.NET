using System.IO.Compression;
using System.Text;
using FluentAssertions;
using iPath.Application.Features.Conversion.Dzi;

namespace iPath.Test.xUnit2.Conversion;

public class DziZipIndexerTests
{
    private const string Descriptor = """<?xml version="1.0" encoding="UTF-8"?><Image TileSize="254" Overlap="1" Format="webp" xmlns="http://schemas.microsoft.com/deepzoom/2008"><Size Width="1000" Height="600"/></Image>""";

    [Fact]
    public void Build_ToolLayout_IndexesDescriptorAndTiles()
    {
        using var zip = CreateZip(CompressionLevel.NoCompression,
            ("slide.dzi", Descriptor),
            ("slide_files/0/0_0.webp", "tile-0"),
            ("slide_files/1/0_0.webp", "tile-1"),
            ("slide_files/2/1_0.webp", "tile-2-1-0"));

        var result = DziZipIndexer.Build(zip);

        result.Success.Should().BeTrue(result.Error);
        result.Index!.TileCount.Should().Be(3);
        result.Index.TileExtension.Should().Be("webp");
        ReadRange(zip, result.Index.Descriptor).Should().Be(Descriptor);
        result.Index.TryGetTile(2, 1, 0, out var range).Should().BeTrue();
        ReadRange(zip, range).Should().Be("tile-2-1-0");
    }

    [Theory]
    [InlineData("slide.dzi", "slide_files/")]
    [InlineData("folder/slide.dzi", "folder/slide_files/")]
    public void Build_DescriptorAtRootOrInSingleFolder_Succeeds(string descriptorPath, string filesPrefix)
    {
        using var zip = CreateZip(CompressionLevel.NoCompression,
            (descriptorPath, Descriptor),
            (filesPrefix + "0/0_0.webp", "tile"));

        DziZipIndexer.Build(zip).Success.Should().BeTrue();
    }

    [Fact]
    public void Build_BackslashEntryNames_AreNormalized()
    {
        using var zip = CreateZip(CompressionLevel.NoCompression,
            ("slide.dzi", Descriptor),
            (@"slide_files\3\2_1.webp", "tile"));

        var result = DziZipIndexer.Build(zip);

        result.Success.Should().BeTrue(result.Error);
        result.Index!.TryGetTile(3, 2, 1, out _).Should().BeTrue();
    }

    [Fact]
    public void Build_NonTileEntries_AreNotIndexed()
    {
        using var zip = CreateZip(CompressionLevel.NoCompression,
            ("slide.dzi", Descriptor),
            ("slide_files/vips-properties.xml", "<properties/>"),
            ("slide_files/0/0_0.webp", "tile"),
            ("slide_files/0/notes.txt", "x"),
            ("__MACOSX/slide.dzi", "resource fork"),
            ("readme.txt", "x"));

        var result = DziZipIndexer.Build(zip);

        result.Success.Should().BeTrue(result.Error);
        result.Index!.TileCount.Should().Be(1);
    }

    [Fact]
    public void Build_DeflatedEntries_RequiresRepack()
    {
        using var zip = CreateZip(CompressionLevel.Optimal,
            ("slide.dzi", Descriptor),
            ("slide_files/0/0_0.webp", new string('a', 4096)));

        var result = DziZipIndexer.Build(zip);

        result.Success.Should().BeFalse();
        result.RequiresRepack.Should().BeTrue();
    }

    [Theory]
    [InlineData("no descriptor", new[] { "slide_files/0/0_0.webp" })]
    [InlineData("two descriptors", new[] { "a.dzi", "b.dzi", "a_files/0/0_0.webp" })]
    [InlineData("descriptor too deep", new[] { "x/y/slide.dzi", "x/y/slide_files/0/0_0.webp" })]
    [InlineData("no tiles", new[] { "slide.dzi" })]
    [InlineData("mixed formats", new[] { "slide.dzi", "slide_files/0/0_0.webp", "slide_files/1/0_0.png" })]
    public void Build_InvalidLayout_Fails(string scenario, string[] names)
    {
        using var zip = CreateZip(CompressionLevel.NoCompression, names.Select(n => (n, "x")).ToArray());

        var result = DziZipIndexer.Build(zip);

        result.Success.Should().BeFalse(scenario);
        result.RequiresRepack.Should().BeFalse(scenario);
        result.Error.Should().NotBeNullOrEmpty(scenario);
    }

    [Fact]
    public void Build_NotAZip_Fails()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 100)));

        DziZipIndexer.Build(stream).Success.Should().BeFalse();
    }

    [Fact]
    public void Build_MoreThan65535Entries_ReadsZip64Directory()
    {
        const int tileCount = 70_000;
        var entries = new List<(string, string)> { ("slide.dzi", Descriptor) };
        for (var i = 0; i < tileCount; i++)
            entries.Add(($"slide_files/16/{i}_0.webp", $"t{i}"));
        using var zip = CreateZip(CompressionLevel.NoCompression, entries.ToArray());

        var result = DziZipIndexer.Build(zip);

        result.Success.Should().BeTrue(result.Error);
        result.Index!.TileCount.Should().Be(tileCount);
        result.Index.TryGetTile(16, tileCount - 1, 0, out var last).Should().BeTrue();
        ReadRange(zip, last).Should().Be($"t{tileCount - 1}");
    }

    [Fact]
    public void FindPreviewTile_ReturnsHighestSingleTileLevel()
    {
        using var zip = CreateZip(CompressionLevel.NoCompression,
            ("slide.dzi", Descriptor),
            ("slide_files/7/0_0.webp", "level-7"),
            ("slide_files/8/0_0.webp", "level-8"),
            ("slide_files/9/0_0.webp", "level-9a"),
            ("slide_files/9/1_0.webp", "level-9b"));
        var index = DziZipIndexer.Build(zip).Index!;

        var preview = index.FindPreviewTile();

        preview.Should().NotBeNull();
        ReadRange(zip, preview!.Value).Should().Be("level-8");
    }

    [Fact]
    public void WriteTo_ReadFrom_RoundTrips()
    {
        using var zip = CreateZip(CompressionLevel.NoCompression,
            ("slide.dzi", Descriptor),
            ("slide_files/0/0_0.webp", "a"),
            ("slide_files/5/3_2.webp", "b"));
        var index = DziZipIndexer.Build(zip).Index!;
        using var buffer = new MemoryStream();

        index.WriteTo(buffer);
        buffer.Position = 0;
        var copy = DziTileIndex.ReadFrom(buffer);

        copy.TileCount.Should().Be(index.TileCount);
        copy.TileExtension.Should().Be(index.TileExtension);
        copy.Descriptor.Should().Be(index.Descriptor);
        copy.TryGetTile(5, 3, 2, out var range).Should().BeTrue();
        index.TryGetTile(5, 3, 2, out var original).Should().BeTrue();
        range.Should().Be(original);
    }

    [Fact]
    public void TryGetTile_UnknownTile_ReturnsFalse()
    {
        using var zip = CreateZip(CompressionLevel.NoCompression,
            ("slide.dzi", Descriptor),
            ("slide_files/0/0_0.webp", "a"));
        var index = DziZipIndexer.Build(zip).Index!;

        index.TryGetTile(0, 1, 0, out _).Should().BeFalse();
        index.TryGetTile(1, 0, 0, out _).Should().BeFalse();
    }

    private static MemoryStream CreateZip(CompressionLevel level, params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, level);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static string ReadRange(Stream zip, ZipRange range)
    {
        var buffer = new byte[range.Length];
        zip.Position = range.Offset;
        zip.ReadExactly(buffer);
        return Encoding.UTF8.GetString(buffer);
    }
}
