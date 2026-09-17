using iPath.Application.Features;
using iPath.Blazor.Componenents.Admin.Groups;

namespace iPath.Test.xUnit2.Groups;

public class CreateGroupCommandModelTests
{
    [Fact]
    public void Clearing_Community_Should_Not_Throw_And_Reset_CommunityId()
    {
        var model = new CreateGroupCommandModel
        {
            Community = new CommunityListDto(Guid.NewGuid(), "TestCommunity")
        };
        model.CommunityId.Should().NotBe(Guid.Empty);

        var act = () => model.Community = null!;

        act.Should().NotThrow();
        model.CommunityId.Should().Be(Guid.Empty);
    }
}
