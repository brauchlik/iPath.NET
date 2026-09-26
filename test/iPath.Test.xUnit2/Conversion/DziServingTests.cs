using System.IO.Compression;
using System.Text;
using FluentAssertions;
using iPath.API.Services.Wsi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace iPath.Test.xUnit2.Conversion;

public class DziFileRequestTests
{
    private static readonly Guid Id = Guid.Parse("0192f3a4-5b6c-7d8e-9f01-23456789abcd");

    [Fact]
    public void Parse_Descriptor_ReturnsDescriptor()
    {
        DziFileRequest.Parse($"{Id}.dzi").Should().Be(new DziFileRequest(Id, DziFileKind.Descriptor));
    }

    [Fact]
    public void Parse_Raw_ReturnsRaw()
    {
        DziFileRequest.Parse(Id.ToString()).Should().Be(new DziFileRequest(Id, DziFileKind.Raw));
    }

    [Theory]
    [InlineData("12/3_4.webp", 12, 3, 4, "webp")]
    [InlineData("0/0_0.JPEG", 0, 0, 0, "jpeg")]
    [InlineData("7/10_2.jpg", 7, 10, 2, "jpg")]
    public void Parse_Tile_ReturnsCoordinates(string tilePath, int level, int column, int row, string extension)
    {
        DziFileRequest.Parse($"{Id}_files/{tilePath}")
            .Should().Be(new DziFileRequest(Id, DziFileKind.Tile, level, column, row, extension));
    }

    [Theory]
    [InlineData("not-a-guid.dzi")]
    [InlineData("0192f3a4-5b6c-7d8e-9f01-23456789abcd_files/vips-properties.xml")]
    [InlineData("0192f3a4-5b6c-7d8e-9f01-23456789abcd_files/12/3_4.xml")]
    [InlineData("0192f3a4-5b6c-7d8e-9f01-23456789abcd_files/../x/0_0.webp")]
    [InlineData("0192f3a4-5b6c-7d8e-9f01-23456789abcd_files/12/3_4.webp/extra")]
    [InlineData("0192f3a4-5b6c-7d8e-9f01-23456789abcd.dzi/extra")]
    public void Parse_AnythingElse_ReturnsNull(string filepath)
    {
        DziFileRequest.Parse(filepath).Should().BeNull();
    }

    [Fact]
    public void LoosePath_Tile_IsBuiltFromParsedParts()
    {
        var request = DziFileRequest.Parse($"{Id}_files/3/1_2.webp")!;

        request.LoosePath("/tmp").Should().Be(Path.Combine("/tmp", $"{Id}_files", "3", "1_2.webp"));
    }
}

public class DziTileIndexCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ipath-tests", Guid.NewGuid().ToString("N"));

    public DziTileIndexCacheTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public async Task GetAsync_DziZip_ReturnsSameIndexOnRepeatedAndConcurrentCalls()
    {
        var zip = WriteDziZip("slide.zip");
        var cache = NewCache();

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.GetAsync(zip, CancellationToken.None)));
        var later = await cache.GetAsync(zip, CancellationToken.None);

        concurrent.Should().AllSatisfy(i => i.Should().BeSameAs(concurrent[0]));
        later.Should().BeSameAs(concurrent[0]);
        later!.TryGetTile(0, 0, 0, out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_NotADziZip_ReturnsNull()
    {
        var path = Path.Combine(_dir, "image.jpg");
        await File.WriteAllBytesAsync(path, new byte[256]);

        (await NewCache().GetAsync(path, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_MissingFile_ReturnsNull()
    {
        (await NewCache().GetAsync(Path.Combine(_dir, "missing"), CancellationToken.None)).Should().BeNull();
    }

    private static DziTileIndexCache NewCache() =>
        new(new MemoryCache(new MemoryCacheOptions()), Substitute.For<ILogger<DziTileIndexCache>>());

    private string WriteDziZip(string name)
    {
        var path = Path.Combine(_dir, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entry, content) in new[] { ("slide.dzi", "<Image/>"), ("slide_files/0/0_0.webp", "tile") })
        {
            using var writer = new StreamWriter(archive.CreateEntry(entry, CompressionLevel.NoCompression).Open());
            writer.Write(content);
        }
        return path;
    }
}

public class FileRangeResultTests
{
    [Fact]
    public async Task ExecuteAsync_WritesOnlyTheRangeWithHeaders()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "0123456789");
            var ctx = new DefaultHttpContext();
            var body = new MemoryStream();
            ctx.Response.Body = body;

            await new FileRangeResult(path, 3, 4, "image/webp").ExecuteAsync(ctx);

            Encoding.ASCII.GetString(body.ToArray()).Should().Be("3456");
            ctx.Response.ContentType.Should().Be("image/webp");
            ctx.Response.ContentLength.Should().Be(4);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
