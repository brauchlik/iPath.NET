using iPath.API.Services.Storage.Migration;
using iPath.Application.Contracts;
using iPath.API.Services.Storage.Providers;
using iPath.Application.Contracts.Storage;
using iPath.Application.Features.Storage;
using iPath.Application.Features.Users;
using iPath.Domain.Config;
using iPath.EF.Core.FeatureHandlers.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace iPath.Test.xUnit2.Storage;

public class StorageMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ipath-tests", Guid.NewGuid().ToString("N"));
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly iPathDbContext _db;
    private readonly StorageRegistry _registry;
    private readonly IUserSession _admin = Substitute.For<IUserSession>();
    private readonly IStorageMigrationQueue _queue = Substitute.For<IStorageMigrationQueue>();
    private readonly StorageConfig _storageConfig;
    private readonly iPathConfig _ipath;

    private readonly Guid _communityId = Guid.CreateVersion7();
    private readonly Guid _groupId = Guid.CreateVersion7();
    private readonly Guid _requestId = Guid.CreateVersion7();
    private readonly Guid _documentId = Guid.CreateVersion7();
    private readonly string _sourceKey;

    public StorageMigrationTests()
    {
        _ipath = new iPathConfig { DataRoot = _root, TempDataPath = Path.Combine(_root, "temp"), LocalDataPath = Path.Combine(_root, "a") };
        _storageConfig = new StorageConfig { Default = "local-a" };
        _storageConfig.Instances["local-a"] = new StorageInstanceConfig { Type = StorageInstanceType.LocalFiles, Path = Path.Combine(_root, "a") };
        _storageConfig.Instances["local-b"] = new StorageInstanceConfig { Type = StorageInstanceType.LocalFiles, Path = Path.Combine(_root, "b") };
        _registry = StorageRegistry.Create(_storageConfig, _ipath, NullLoggerFactory.Instance);

        _admin.User.Returns(new SessionUserDto(Guid.NewGuid(), "admin", "admin@test.com", "A", ["Admin"], null, null));

        _connection.Open();
        _db = new iPathDbContext(new DbContextOptionsBuilder<iPathDbContext>().UseSqlite(_connection).Options,
            Substitute.For<IMediator>(), Substitute.For<IConfiguration>());
        _db.Database.EnsureCreated();

        var ownerId = Guid.NewGuid();
        _sourceKey = StorageKeys.ForDocument(_groupId, _requestId, _documentId);
        _db.Users.Add(new User { Id = ownerId, UserName = "owner" });
        var community = Community.Create("Pathology", ownerId);
        community.Id = _communityId;
        _db.Communities.Add(community);
        var group = Group.Create("Hematology", ownerId, _communityId);
        group.Id = _groupId;
        _db.Groups.Add(group);
        _db.ServiceRequests.Add(new ServiceRequest { Id = _requestId, GroupId = _groupId, OwnerId = ownerId, NodeType = "Test", Description = new RequestDescription { Title = "case" } });
        _db.Documents.Add(new DocumentNode
        {
            Id = _documentId, ServiceRequestId = _requestId, OwnerId = ownerId, DocumentType = "wsi",
            File = new NodeFile { Filename = "slide.dzi", MimeType = "application/zip", FileSize = 12, Storage = new StorageInfo("local-a", _sourceKey) },
        });
        _db.SaveChanges();

        WriteObject("a", _sourceKey, "slide-bytes!");
        WriteObject("a", StorageKeys.TileIndexFor(_sourceKey), "index");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        _registry.Dispose();
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public async Task ChangeCommunityStorage_SwitchesCommunityAndPlansOneItemPerFile()
    {
        var dto = await ChangeStorageAsync("local-b");

        dto.Should().NotBeNull();
        dto!.Total.Should().Be(1);
        (await _db.Communities.AsNoTracking().SingleAsync()).Settings.StorageInstance.Should().Be("local-b");
        await _queue.Received(1).EnqueueAsync(dto.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeCommunityStorage_FilesAlreadyOnTarget_CreatesNoMigration()
    {
        var dto = await ChangeStorageAsync("local-a");

        dto.Should().BeNull();
        (await _db.StorageMigrations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Run_CopiesVerifiesSwitchesAndRetiresSource()
    {
        var dto = await ChangeStorageAsync("local-b");

        await NewProcessor().RunAsync(dto!.Id, default);

        var document = await _db.Documents.AsNoTracking().SingleAsync();
        document.File.Storage!.ProviderName.Should().Be("local-b");
        document.File.Storage.StorageId.Should().Be(_sourceKey);
        document.File.RetiredLocations.Should().ContainSingle(l => l.ProviderName == "local-a" && l.StorageId == _sourceKey);
        ReadObject("b", _sourceKey).Should().Be("slide-bytes!");
        ReadObject("b", StorageKeys.TileIndexFor(_sourceKey)).Should().Be("index");
        ReadObject("a", _sourceKey).Should().Be("slide-bytes!", "the source stays until the migration is purged");

        var migration = await _db.StorageMigrations.AsNoTracking().Include(m => m.Items).SingleAsync();
        migration.Status.Should().Be(StorageMigrationStatus.Completed);
        migration.Items.Single().Status.Should().Be(StorageMigrationItemStatus.Switched);
        migration.Items.Single().Sha256.Should().HaveLength(64);
        File.Exists(Path.Combine(migration.BackupPath!, _sourceKey.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
    }

    [Fact]
    public async Task Run_Twice_DoesNotMoveAgain()
    {
        var dto = await ChangeStorageAsync("local-b");
        await NewProcessor().RunAsync(dto!.Id, default);

        await NewProcessor().RunAsync(dto.Id, default);

        (await _db.Documents.AsNoTracking().SingleAsync()).File.RetiredLocations.Should().HaveCount(1);
    }

    [Fact]
    public async Task Run_MissingSource_FailsItemAndKeepsDocumentOnSource()
    {
        File.Delete(Path.Combine(_root, "a", _sourceKey.Replace('/', Path.DirectorySeparatorChar)));
        var dto = await ChangeStorageAsync("local-b");
        var migration = await _db.StorageMigrations.Include(m => m.Items).SingleAsync();
        migration.Items.Single().Attempts = 2; // skip the retry back-off: one attempt left

        await NewProcessor().RunAsync(dto!.Id, default);

        var reloaded = await _db.StorageMigrations.AsNoTracking().Include(m => m.Items).SingleAsync();
        reloaded.Status.Should().Be(StorageMigrationStatus.CompletedWithErrors);
        reloaded.Items.Single().Status.Should().Be(StorageMigrationItemStatus.Failed);
        (await _db.Documents.AsNoTracking().SingleAsync()).File.Storage!.ProviderName.Should().Be("local-a");
    }

    [Fact]
    public async Task Purge_DeletesRetiredSourceAndBackup()
    {
        var dto = await ChangeStorageAsync("local-b");
        await NewProcessor().RunAsync(dto!.Id, default);
        var backup = (await _db.StorageMigrations.AsNoTracking().SingleAsync()).BackupPath!;

        var purged = await new PurgeStorageMigrationHandler(_db, _registry, _admin, NullLogger<PurgeStorageMigrationHandler>.Instance)
            .Handle(new PurgeStorageMigrationCommand(dto.Id), default);

        purged.Status.Should().Be(nameof(StorageMigrationStatus.Purged));
        File.Exists(Path.Combine(_root, "a", _sourceKey.Replace('/', Path.DirectorySeparatorChar))).Should().BeFalse();
        Directory.Exists(backup).Should().BeFalse();
        (await _db.Documents.AsNoTracking().SingleAsync()).File.RetiredLocations.Should().BeEmpty();
        ReadObject("b", _sourceKey).Should().Be("slide-bytes!");
    }

    [Fact]
    public async Task Purge_RunningMigration_IsRefused()
    {
        var dto = await ChangeStorageAsync("local-b");

        var act = () => new PurgeStorageMigrationHandler(_db, _registry, _admin, NullLogger<PurgeStorageMigrationHandler>.Instance)
            .Handle(new PurgeStorageMigrationCommand(dto!.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private async Task<StorageMigrationDto?> ChangeStorageAsync(string instance) =>
        await new ChangeCommunityStorageHandler(_db, _registry, new StorageMigrationPlanner(_db, _registry, _admin), _queue, _admin)
            .Handle(new ChangeCommunityStorageCommand(_communityId, instance), default);

    private StorageMigrationProcessor NewProcessor() => new(_db, _registry, new MemoryCache(new MemoryCacheOptions()),
        Options.Create(_storageConfig), Options.Create(_ipath), NullLogger<StorageMigrationProcessor>.Instance);

    private void WriteObject(string instanceFolder, string key, string content)
    {
        var path = Path.Combine(_root, instanceFolder, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string ReadObject(string instanceFolder, string key) =>
        File.ReadAllText(Path.Combine(_root, instanceFolder, key.Replace('/', Path.DirectorySeparatorChar)));
}
