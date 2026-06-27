using iPath.API.Services.CaseRoom;
using iPath.API.Services.Notifications;
using iPath.Application.Contracts;
using iPath.Application.Features;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using iPath.Application.Features.Users;
using iPath.Application.Localization;
using iPath.Blazor.ServiceLib.Services;
using iPath.Domain.Config;
using DispatchR;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace iPath.Test.xUnit2.CaseRoom;

public class DirectApiClientCaseRoomTests
{
    private static (DirectApiClient client, CaseRoomSessionStore store) CreateClient()
    {
        var store = new CaseRoomSessionStore(
            Substitute.For<ISseConnectionManager>(),
            new NotificationEventBus(),
            new LoggerFactory().CreateLogger<CaseRoomSessionStore>());

        var testUserId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        var userSession = Substitute.For<IUserSession>();
        userSession.User.Returns(new SessionUserDto(testUserId, "Test", "test@test.com", "TT", new[] { "Admin" }, null, null));

        var opts = Substitute.For<IOptions<iPathClientConfig>>();
        opts.Value.Returns(new iPathClientConfig());

        var client = new DirectApiClient(
            mediator: mediator,
            groupService: Substitute.For<IGroupService>(),
            emailRepo: Substitute.For<IEmailRepository>(),
            notificationRepo: Substitute.For<INotificationRepository>(),
            userSession: userSession,
            localization: Substitute.For<ILocalizationDataProvider>(),
            config: opts,
            logger: new LoggerFactory().CreateLogger<DirectApiClient>(),
            caseRoomStore: store);

        return (client, store);
    }

    [Fact]
    public async Task DirectApiClient_JoinCaseRoom_ReturnsSnapshotFromStore()
    {
        var (client, store) = CreateClient();
        var requestId = Guid.NewGuid();

        var resp = await client.JoinCaseRoom(requestId);

        resp.IsSuccessful.Should().BeTrue();
        resp.Content!.RequestId.Should().Be(requestId);
        resp.Content.Participants.Should().ContainSingle();
    }

    [Fact]
    public async Task DirectApiClient_SyncCaseRoom_PersistsViewport()
    {
        var (client, store) = CreateClient();
        var requestId = Guid.NewGuid();
        await client.JoinCaseRoom(requestId);

        await client.SyncCaseRoom(requestId, new SyncPayload(null, new ViewportState(0.1, 0.2, 0.3)));

        var status = await client.GetCaseRoomStatus(requestId);
        status.Content!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DirectApiClient_GetCaseRoomStatus_ReturnsNullWhenNoSession()
    {
        var (client, _) = CreateClient();
        var resp = await client.GetCaseRoomStatus(Guid.NewGuid());
        resp.Content.Should().BeNull();
    }
}
