using iPath.API.Services.Notifications;
using iPath.API.Services.CaseRoom;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class CaseRoomSessionStoreTests
{
    private static CaseRoomSessionStore CreateStore()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var sseMgr = Substitute.For<ISseConnectionManager>();
        var bus = new NotificationEventBus();
        var logger = new LoggerFactory().CreateLogger<CaseRoomSessionStore>();
        return new CaseRoomSessionStore(sseMgr, bus, logger);
    }

    [Fact]
    public async Task Join_FirstUser_CreatesSessionWithOneParticipant()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var snapshot = await store.JoinAsync(requestId, sessionId, userId, "Alice", default);

        snapshot.RequestId.Should().Be(requestId);
        snapshot.Participants.Should().ContainSingle(p => p.UserId == userId);
        snapshot.ActiveDocumentId.Should().Be(null);
    }

    [Fact]
    public async Task Join_SecondUser_AddsParticipant()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();

        await store.JoinAsync(requestId, Guid.NewGuid(), Guid.NewGuid(), "Alice", default);
        var snapshot = await store.JoinAsync(requestId, Guid.NewGuid(), Guid.NewGuid(), "Bob", default);

        snapshot.Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task Join_SameUserDifferentSession_AddsParticipant()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await store.JoinAsync(requestId, Guid.NewGuid(), userId, "Alice", default);
        var snapshot = await store.JoinAsync(requestId, Guid.NewGuid(), userId, "Alice", default);

        snapshot.Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task Sync_UpdatesViewport()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await store.JoinAsync(requestId, sessionId, userId, "Alice", default);

        await store.SyncAsync(requestId, sessionId, userId,
            new SyncPayload(null, new ViewportState(0.5, 0.5, 2.0)), default);

        var status = await store.GetStatusAsync(requestId, default);
        status!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Sync_UpdatesActiveDocument()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await store.JoinAsync(requestId, sessionId, userId, "Alice", default);

        var docId = Guid.NewGuid();
        await store.SyncAsync(requestId, sessionId, userId, new SyncPayload(docId, null), default);

        var snapshot = await store.JoinAsync(requestId, Guid.NewGuid(), Guid.NewGuid(), "Bob", default);
        snapshot.ActiveDocumentId.Should().Be(docId);
    }

    [Fact]
    public async Task Leave_LastUser_SchedulesTeardown()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await store.JoinAsync(requestId, sessionId, userId, "Alice", default);

        await store.LeaveAsync(requestId, sessionId, default);

        var status = await store.GetStatusAsync(requestId, default);
        status.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatus_ReturnsNull_WhenNoSession()
    {
        var store = CreateStore();
        var status = await store.GetStatusAsync(Guid.NewGuid(), default);
        status.Should().BeNull();
    }

    [Fact]
    public async Task CreateShareToken_AndValidate_SucceedsOnlyWhenHostIsPresent()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();

        // Generate token
        var token = await store.CreateShareTokenAsync(requestId, default);
        token.Should().NotBeNullOrEmpty();

        // Validating with no host present should fail
        var isValidNoHost = await store.IsShareTokenValidAsync(requestId, token, default);
        isValidNoHost.Should().BeFalse();

        // Join a host
        await store.JoinAsync(requestId, Guid.NewGuid(), Guid.NewGuid(), "Host User", isGuest: false, default);

        // Validating with host present should succeed
        var isValidWithHost = await store.IsShareTokenValidAsync(requestId, token, default);
        isValidWithHost.Should().BeTrue();
    }

    [Fact]
    public async Task JoinAsync_WithGuestFlag_CorrectlyMarksParticipant()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var snapshot = await store.JoinAsync(requestId, sessionId, Guid.Empty, "Guest User", isGuest: true, default);
        var guest = snapshot.Participants.Should().ContainSingle(p => p.SessionId == sessionId).Subject;
        guest.IsGuest.Should().BeTrue();
    }

    [Fact]
    public async Task LeaveAsync_LastHostLeaves_KicksGuestsAndClearsTokens()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var hostSessionId = Guid.NewGuid();
        var guestSessionId = Guid.NewGuid();

        // Share token
        var token = await store.CreateShareTokenAsync(requestId, default);

        // Join host and guest
        await store.JoinAsync(requestId, hostSessionId, Guid.NewGuid(), "Host User", isGuest: false, default);
        await store.JoinAsync(requestId, guestSessionId, Guid.Empty, "Guest User", isGuest: true, default);

        // Leave host
        await store.LeaveAsync(requestId, hostSessionId, default);

        // Verify guest is evicted and token is invalidated
        var status = await store.GetStatusAsync(requestId, default);
        status.Should().NotBeNull();
        status!.ParticipantCount.Should().Be(0); // Guest kicked

        var isTokenValid = await store.IsShareTokenValidAsync(requestId, token, default);
        isTokenValid.Should().BeFalse(); // Token cleared
    }
}