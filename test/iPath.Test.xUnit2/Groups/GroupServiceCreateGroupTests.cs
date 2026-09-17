using iPath.Application.Contracts;
using iPath.Application.Features;
using iPath.EF.Core.FeatureHandlers.Groups;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace iPath.Test.xUnit2.Groups;

public class GroupServiceCreateGroupTests
{
    private readonly iPathDbContext _db;
    private readonly IUserSession _sess;

    public GroupServiceCreateGroupTests()
    {
        var opts = new DbContextOptionsBuilder<iPathDbContext>()
            .UseInMemoryDatabase($"GroupCreateTest_{Guid.NewGuid()}")
            .Options;
        _db = new iPathDbContext(opts, Substitute.For<IMediator>(), Substitute.For<IConfiguration>());
        _sess = Substitute.For<IUserSession>();
    }

    [Fact]
    public async Task CreateGroup_Should_Assign_Owner_As_Moderator()
    {
        var ownerId = Guid.NewGuid();
        var owner = new User { Id = ownerId, UserName = "owner" };
        var community = Community.Create("TestCommunity", ownerId);
        _db.Users.Add(owner);
        _db.Communities.Add(community);
        await _db.SaveChangesAsync();

        var svc = new GroupService(_db, _sess, Substitute.For<IMediator>(), NullLogger<GroupService>.Instance);

        var dto = await svc.CreateGroupAsync(new CreateGroupCommand
        {
            Name = "New Group",
            OwnerId = ownerId,
            CommunityId = community.Id,
            Settings = new GroupSettings()
        });

        dto.Should().NotBeNull();

        var members = await _db.Set<GroupMember>()
            .Where(m => m.GroupId == dto.Id)
            .Select(m => new { m.UserId, m.Role })
            .ToListAsync();
        members.Should().ContainSingle();
        members.Single().UserId.Should().Be(ownerId);
        members.Single().Role.Should().Be(eMemberRole.Moderator);
    }
}
