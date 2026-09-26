using iPath.Application.Contracts;
using iPath.Application.Features;
using iPath.Application.Features.Users;
using iPath.Domain.Config;
using iPath.EF.Core.FeatureHandlers.Documents.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace iPath.Test.xUnit2.Documents;

public class GetDocumentFileHandlerTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "ipath-tests", Guid.NewGuid().ToString("N"));
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection = new("DataSource=:memory:");
    private readonly iPathDbContext _db;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly IUserSession _sess = Substitute.For<IUserSession>();
    private readonly Guid _groupId = Guid.CreateVersion7();
    private readonly Guid _documentId = Guid.CreateVersion7();

    public GetDocumentFileHandlerTests()
    {
        Directory.CreateDirectory(_temp);
        // SQLite, not the InMemory provider: InMemory cannot materialise NodeFile's JSON column.
        _connection.Open();
        var opts = new DbContextOptionsBuilder<iPathDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new iPathDbContext(opts, Substitute.For<IMediator>(), Substitute.For<IConfiguration>());
        _db.Database.EnsureCreated();

        var ownerId = Guid.NewGuid();
        var requestId = Guid.CreateVersion7();
        _db.Users.Add(new User { Id = ownerId, UserName = "owner" });
        _db.Groups.Add(new Group { Id = _groupId });
        _db.ServiceRequests.Add(new ServiceRequest { Id = requestId, GroupId = _groupId, OwnerId = ownerId, NodeType = "Test", Description = new RequestDescription { Title = "case" } });
        _db.Documents.Add(new DocumentNode { Id = _documentId, ServiceRequestId = requestId, OwnerId = ownerId, DocumentType = "file", File = new NodeFile { Filename = "a.txt", MimeType = "text/plain" } });
        _db.SaveChanges();

        File.WriteAllText(Path.Combine(_temp, _documentId.ToString()), "content");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_temp, true); } catch { }
    }

    [Fact]
    public async Task Handle_RepeatedRequests_ServeFromCachedMetadataWithoutDatabase()
    {
        SignInAsMemberOf(_groupId);
        var handler = CreateHandler();
        (await handler.Handle(new GetDocumentFileQuery(_documentId), default)).ServePath.Should().NotBeNull();

        _db.Documents.Remove(_db.Documents.Single());
        await _db.SaveChangesAsync();
        var second = await handler.Handle(new GetDocumentFileQuery(_documentId), default);

        second.NotFound.Should().BeFalse("the document metadata is cached for a short time");
        second.ServePath.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_CachedMetadata_StillChecksAccessOnEveryRequest()
    {
        SignInAsMemberOf(_groupId);
        var handler = CreateHandler();
        await handler.Handle(new GetDocumentFileQuery(_documentId), default);

        SignInAsMemberOf(Guid.CreateVersion7());
        var result = await handler.Handle(new GetDocumentFileQuery(_documentId), default);

        result.AccessDenied.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnknownDocument_ReturnsNotFound()
    {
        SignInAsMemberOf(_groupId);

        var result = await CreateHandler().Handle(new GetDocumentFileQuery(Guid.CreateVersion7()), default);

        result.NotFound.Should().BeTrue();
    }

    private GetDocumentFileHandler CreateHandler() => new(
        _db,
        Substitute.For<IRemoteStorageService>(),
        _sess,
        Options.Create(new iPathConfig { TempDataPath = _temp, LocalDataPath = _temp }),
        _cache,
        new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

    private void SignInAsMemberOf(Guid groupId) =>
        _sess.User.Returns(new SessionUserDto(Guid.NewGuid(), "user", "user@test.com", "U", [], null,
            [new UserGroupMemberDto(groupId, "group", eMemberRole.User, false)]));
}
