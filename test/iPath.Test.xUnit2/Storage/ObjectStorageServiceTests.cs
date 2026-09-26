using iPath.API.Services.Storage;
using iPath.API.Services.Storage.Providers;
using iPath.Application.Contracts.Storage;
using iPath.Domain.Config;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace iPath.Test.xUnit2.Storage;

public class ObjectStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ipath-tests", Guid.NewGuid().ToString("N"));
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly iPathDbContext _db;
    private readonly StorageRegistry _registry;
    private readonly ObjectStorageService _service;
    private readonly Guid _groupId = Guid.CreateVersion7();
    private readonly Guid _requestId = Guid.CreateVersion7();
    private readonly Guid _documentId = Guid.CreateVersion7();

    public ObjectStorageServiceTests()
    {
        var temp = Path.Combine(_root, "temp");
        Directory.CreateDirectory(temp);
        var ipath = new iPathConfig { TempDataPath = temp, LocalDataPath = Path.Combine(_root, "data") };

        _connection.Open();
        _db = new iPathDbContext(new DbContextOptionsBuilder<iPathDbContext>().UseSqlite(_connection).Options,
            Substitute.For<IMediator>(), Substitute.For<IConfiguration>());
        _db.Database.EnsureCreated();

        var ownerId = Guid.NewGuid();
        _db.Users.Add(new User { Id = ownerId, UserName = "owner" });
        _db.Groups.Add(new Group { Id = _groupId });
        _db.ServiceRequests.Add(new ServiceRequest { Id = _requestId, GroupId = _groupId, OwnerId = ownerId, NodeType = "Test", Description = new RequestDescription { Title = "case" } });
        _db.Documents.Add(new DocumentNode { Id = _documentId, ServiceRequestId = _requestId, OwnerId = ownerId, DocumentType = "wsi", File = new NodeFile { Filename = "slide.dzi", MimeType = "application/zip" } });
        _db.SaveChanges();

        _registry = StorageRegistry.Create(new StorageConfig(), ipath, NullLoggerFactory.Instance);
        _registry.Default.EnsureReadyAsync(default).Wait();
        _service = new ObjectStorageService(_registry, _db, Options.Create(ipath), NullLogger<ObjectStorageService>.Instance);

        File.WriteAllText(Path.Combine(temp, _documentId.ToString()), "zip-bytes");
        File.WriteAllText(Path.Combine(temp, _documentId + ".tileindex"), "index-bytes");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        _registry.Dispose();
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public async Task PutFileAsync_StoresFileAndTileIndexUnderDocumentKey()
    {
        var result = await _service.PutFileAsync(_documentId);

        result.Success.Should().BeTrue(result.Message);
        var key = StorageKeys.ForDocument(_groupId, _requestId, _documentId);
        result.Storage.Should().BeEquivalentTo(new StorageInfo("LocalFiles", key), o => o.Excluding(s => s.UpdatedOn));
        File.ReadAllText(_registry.Default.GetLocalPath(key)!).Should().Be("zip-bytes");
        File.ReadAllText(_registry.Default.GetLocalPath(StorageKeys.TileIndexFor(key))!).Should().Be("index-bytes");
    }

    [Fact]
    public async Task GetFileAsync_RestoresTheTempCopy()
    {
        await _service.PutFileAsync(_documentId);
        var tempFile = Path.Combine(_root, "temp", _documentId.ToString());
        File.Delete(tempFile);

        var result = await _service.GetFileAsync(_documentId);

        result.Success.Should().BeTrue(result.Message);
        File.ReadAllText(tempFile).Should().Be("zip-bytes");
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesFileAndTileIndex()
    {
        var stored = await _service.PutFileAsync(_documentId);
        var key = stored.Storage!.StorageId;

        var result = await _service.DeleteFileAsync(_documentId);

        result.Success.Should().BeTrue(result.Message);
        _registry.Default.GetLocalPath(key).Should().BeNull();
        _registry.Default.GetLocalPath(StorageKeys.TileIndexFor(key)).Should().BeNull();
    }
}
