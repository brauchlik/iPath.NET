using System.Security.Claims;
using iPath.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace iPath.Test.xUnit2.Tests;

public class UserSessionReloadTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly iPathDbContext _db;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly UserManager<User> _userManager = Substitute.For<UserManager<User>>(
        Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
    private readonly User _user = new() { Id = Guid.CreateVersion7(), UserName = "anna", Email = "anna@test.com" };

    public UserSessionReloadTests()
    {
        // SQLite rather than InMemory: LoadUser projects memberships through navigations.
        _connection.Open();
        _db = new iPathDbContext(
            new DbContextOptionsBuilder<iPathDbContext>().UseSqlite(_connection).Options,
            Substitute.For<IMediator>(), Substitute.For<IConfiguration>());
        _db.Database.EnsureCreated();

        _userManager.FindByIdAsync(_user.Id.ToString()).Returns(_user);
        GivenRoles("User");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void User_WithoutReload_ServesCachedSessionAcrossRequests()
    {
        NewRequestSession().User!.roles.Should().Equal("User");
        GivenRoles("User", "Admin");

        NewRequestSession().User!.roles.Should().Equal("User");
    }

    [Fact]
    public void ReloadUser_NextRequestLoadsFreshSession()
    {
        NewRequestSession().User!.roles.Should().Equal("User");
        GivenRoles("User", "Admin");

        NewRequestSession().ReloadUser(_user.Id);

        NewRequestSession().User!.roles.Should().Equal("User", "Admin");
    }

    [Fact]
    public void ReloadUser_SameRequest_ReloadsOnNextAccess()
    {
        var session = NewRequestSession();
        session.User!.roles.Should().Equal("User");
        GivenRoles("User", "Admin");

        session.ReloadUser(_user.Id);

        session.User!.roles.Should().Equal("User", "Admin");
    }

    [Fact]
    public void ReloadUser_OtherUser_KeepsCachedSession()
    {
        NewRequestSession().User!.roles.Should().Equal("User");
        GivenRoles("User", "Admin");

        NewRequestSession().ReloadUser(Guid.CreateVersion7());

        NewRequestSession().User!.roles.Should().Equal("User");
    }

    private void GivenRoles(params string[] roles) =>
        _userManager.GetRolesAsync(_user).Returns(roles.ToList());

    private UserSession NewRequestSession()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, _user.Id.ToString())], authenticationType: "Test"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new UserSession(_db, _userManager, _cache, accessor, NullLogger<UserSession>.Instance);
    }
}
