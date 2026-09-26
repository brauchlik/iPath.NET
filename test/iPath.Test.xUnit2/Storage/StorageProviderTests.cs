using System.Text;
using iPath.API.Services.Storage.Providers;
using iPath.API.Services.Wsi;
using iPath.Application.Contracts.Storage;
using iPath.Domain.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace iPath.Test.xUnit2.Storage;

public class StorageKeysTests
{
    private static readonly Guid Group = Guid.Parse("0192f3a4-0000-7000-8000-000000000001");
    private static readonly Guid Request = Guid.Parse("0192f3a4-0000-7000-8000-000000000002");
    private static readonly Guid Document = Guid.Parse("0192f3a4-0000-7000-8000-000000000003");

    [Fact]
    public void ForDocument_UsesGroupRequestDocumentVariantLayout()
    {
        StorageKeys.ForDocument(Group, Request, Document).Should().Be($"{Group}/{Request}/{Document}/original");
    }

    [Fact]
    public void Resolve_StoredKey_IsUsedAsIs()
    {
        var key = StorageKeys.ForDocument(Group, Request, Document);

        StorageKeys.Resolve(new StorageInfo("local-main", key), Guid.NewGuid(), Guid.NewGuid()).Should().Be(key);
    }

    [Fact]
    public void Resolve_LegacyFileName_IsPlacedUnderTheCaseFolder()
    {
        StorageKeys.Resolve(new StorageInfo("LocalFiles", "abc"), Group, Request).Should().Be($"{Group}/{Request}/abc");
    }
}

public class LocalFileStorageProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ipath-tests", Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorageProvider _provider;

    public LocalFileStorageProviderTests()
    {
        _provider = new LocalFileStorageProvider("local", _root);
        _provider.EnsureReadyAsync(default).Wait();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public async Task PutFileAsync_StoresUnderKey_AndReadsBack()
    {
        var source = WriteSource("0123456789");

        await _provider.PutFileAsync("g/r/d/original", source, null, default);

        (await _provider.GetLengthAsync("g/r/d/original", default)).Should().Be(10);
        await using var stream = await _provider.OpenReadAsync("g/r/d/original", default);
        new StreamReader(stream).ReadToEnd().Should().Be("0123456789");
        File.Exists(Path.Combine(_root, "g", "r", "d", "original")).Should().BeTrue();
    }

    [Fact]
    public async Task GetRange_ReturnsPhysicalFileRange()
    {
        await _provider.PutFileAsync("k", WriteSource("0123456789"), null, default);

        _provider.GetRange("k", 3, 4).Should().Be(new PhysicalFileRange(Path.Combine(_root, "k"), 3, 4));
    }

    [Fact]
    public async Task DeleteAsync_RemovesObject()
    {
        await _provider.PutFileAsync("k", WriteSource("x"), null, default);

        await _provider.DeleteAsync("k", default);

        (await _provider.GetLengthAsync("k", default)).Should().BeNull();
        _provider.GetLocalPath("k").Should().BeNull();
    }

    [Fact]
    public async Task OpenReadAsync_MissingKey_ThrowsFileNotFound()
    {
        var act = () => _provider.OpenReadAsync("missing", default);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("a/../../outside")]
    public void Keys_EscapingTheRoot_AreRejected(string key)
    {
        var act = () => _provider.GetLocalPath(key);

        act.Should().Throw<ArgumentException>();
    }

    private string WriteSource(string content)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".src");
        File.WriteAllText(path, content);
        return path;
    }
}

public class StorageRegistryTests
{
    private static readonly iPathConfig IPath = new() { LocalDataPath = Path.Combine(Path.GetTempPath(), "ipath-tests", "registry") };

    [Fact]
    public void Create_WithoutInstances_UsesLocalDataPathAsLocalFiles()
    {
        using var registry = StorageRegistry.Create(new StorageConfig(), IPath, NullLoggerFactory.Instance);

        registry.Default.InstanceName.Should().Be("LocalFiles");
        registry.Default.Type.Should().Be(StorageInstanceType.LocalFiles);
        registry.Default.Description.Should().Be(Path.GetFullPath(IPath.LocalDataPath));
    }

    [Fact]
    public void Resolve_LegacyLocalFilesName_MapsToTheLocalInstance()
    {
        using var registry = StorageRegistry.Create(Config("local-main", ("local-main", Local())), IPath, NullLoggerFactory.Instance);

        registry.Resolve("LocalFiles").Should().BeSameAs(registry.Default);
        registry.Resolve("local-main").Should().BeSameAs(registry.Default);
    }

    [Fact]
    public void Resolve_UnknownInstance_ReturnsNull()
    {
        using var registry = StorageRegistry.Create(new StorageConfig(), IPath, NullLoggerFactory.Instance);

        registry.Resolve("GoogleDrive").Should().BeNull();
        registry.Resolve(null).Should().BeNull();
    }

    [Fact]
    public void Create_S3Instance_BuildsS3Provider()
    {
        using var registry = StorageRegistry.Create(
            Config("s3", ("s3", new StorageInstanceConfig { Type = StorageInstanceType.S3, ServiceUrl = "http://localhost:9000", Bucket = "b", AccessKey = "a", SecretKey = "s" })),
            IPath, NullLoggerFactory.Instance);

        registry.Default.Type.Should().Be(StorageInstanceType.S3);
        registry.Default.Description.Should().Be("http://localhost:9000/b");
    }

    [Fact]
    public void Create_DefaultNotConfigured_Throws()
    {
        var act = () => StorageRegistry.Create(Config("missing", ("a", Local()), ("b", Local())), IPath, NullLoggerFactory.Instance);

        act.Should().Throw<InvalidOperationException>().WithMessage("*missing*");
    }

    [Fact]
    public void Create_S3WithoutBucket_Throws()
    {
        var act = () => StorageRegistry.Create(
            Config("s3", ("s3", new StorageInstanceConfig { Type = StorageInstanceType.S3, AccessKey = "a", SecretKey = "s" })),
            IPath, NullLoggerFactory.Instance);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Bucket*");
    }

    private static StorageInstanceConfig Local() => new() { Type = StorageInstanceType.LocalFiles, Path = IPath.LocalDataPath };

    private static StorageConfig Config(string defaultName, params (string Name, StorageInstanceConfig Cfg)[] instances)
    {
        var cfg = new StorageConfig { Default = defaultName };
        foreach (var (name, instance) in instances)
            cfg.Instances[name] = instance;
        return cfg;
    }
}

public class RemoteFileResultRangeTests
{
    [Theory]
    [InlineData(null, 0, 1000, false)]
    [InlineData("bytes=0-99", 0, 100, true)]
    [InlineData("bytes=900-", 900, 100, true)]
    [InlineData("bytes=-100", 900, 100, true)]
    [InlineData("bytes=950-2000", 950, 50, true)]
    [InlineData("bytes=0-9,20-29", 0, 1000, false)]
    [InlineData("items=0-9", 0, 1000, false)]
    public void ParseRange_ValidOrIgnored(string? header, long offset, long count, bool partial)
    {
        RemoteFileResult.ParseRange(Request(header), 1000).Should().Be((offset, count, partial));
    }

    [Fact]
    public void ParseRange_BeyondEnd_IsNotSatisfiable()
    {
        RemoteFileResult.ParseRange(Request("bytes=1000-1100"), 1000).Count.Should().Be(-1);
    }

    private static HttpRequest Request(string? range)
    {
        var ctx = new DefaultHttpContext();
        if (range is not null)
            ctx.Request.Headers.Range = range;
        return ctx.Request;
    }
}

/// <summary>
/// Runs only against a real S3 endpoint, e.g. the RustFS dev container:
/// IPATH_TEST_S3_URL=http://localhost:9000, IPATH_TEST_S3_ACCESS_KEY, IPATH_TEST_S3_SECRET_KEY.
/// </summary>
public sealed class S3FactAttribute : FactAttribute
{
    public S3FactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IPATH_TEST_S3_URL")))
            Skip = "Set IPATH_TEST_S3_URL, IPATH_TEST_S3_ACCESS_KEY and IPATH_TEST_S3_SECRET_KEY to run S3 tests.";
    }
}

public class S3StorageProviderTests
{
    [S3Fact]
    public async Task RoundTrip_PutRangeReadDelete()
    {
        using var provider = new S3StorageProvider("s3-test", new StorageInstanceConfig
        {
            Type = StorageInstanceType.S3,
            ServiceUrl = Environment.GetEnvironmentVariable("IPATH_TEST_S3_URL"),
            AccessKey = Environment.GetEnvironmentVariable("IPATH_TEST_S3_ACCESS_KEY"),
            SecretKey = Environment.GetEnvironmentVariable("IPATH_TEST_S3_SECRET_KEY"),
            Bucket = "ipath-test",
        }, NullLogger.Instance);
        var key = $"tests/{Guid.NewGuid()}/original";
        var source = Path.GetTempFileName();
        await File.WriteAllTextAsync(source, "0123456789");

        try
        {
            await provider.EnsureReadyAsync(default);
            await provider.PutFileAsync(key, source, "text/plain", default);

            (await provider.GetLengthAsync(key, default)).Should().Be(10);
            var range = provider.GetRange(key, 3, 4).Should().BeOfType<StreamRange>().Subject;
            await using (var stream = await range.Open(default))
                new StreamReader(stream, Encoding.ASCII).ReadToEnd().Should().Be("3456");

            await provider.DeleteAsync(key, default);
            (await provider.GetLengthAsync(key, default)).Should().BeNull();
        }
        finally
        {
            File.Delete(source);
        }
    }
}

public class NodeFileRetiredLocationsTests
{
    [Fact]
    public void RetiredLocations_SetToNull_ReadsAsEmptyAndClones()
    {
        // Files stored before RetiredLocations existed load the list as null from their JSON.
        var file = new NodeFile { RetiredLocations = null! };

        file.RetiredLocations.Should().BeEmpty();
        file.Clone().RetiredLocations.Should().BeEmpty();
    }

    [Fact]
    public void IsLegacy_FileNameOnly_IsLegacy()
    {
        StorageKeys.IsLegacy(new StorageInfo("LocalFiles", "abc")).Should().BeTrue();
        StorageKeys.IsLegacy(new StorageInfo("local-main", "g/r/d/original")).Should().BeFalse();
    }
}
