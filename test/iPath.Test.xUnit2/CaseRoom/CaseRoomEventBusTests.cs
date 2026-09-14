using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class CaseRoomEventBusTests
{
    private static CaseRoomSyncEvent Evt(Guid requestId, string displayName) =>
        new(requestId, Guid.NewGuid(), displayName,
            new SyncPayload(null, new ViewportState(0.1, 0.2, 0.3)), DateTimeOffset.UtcNow);

    [Fact]
    public void SubscribeCaseRoomSync_ReceivesOnlyItsOwnRoom()
    {
        var bus = new NotificationEventBus();
        var requestId = Guid.NewGuid();
        var received = new List<CaseRoomSyncEvent>();

        // Deliberately unfiltered: the bus must not deliver other rooms' events at all.
        var sub = bus.SubscribeCaseRoomSync(requestId, received.Add);

        bus.PublishCaseRoomSync(Evt(requestId, "Alice"));
        bus.PublishCaseRoomSync(Evt(Guid.NewGuid(), "Bob"));

        received.Should().ContainSingle();
        received[0].DisplayName.Should().Be("Alice");
        sub.Dispose();
    }

    [Fact]
    public void Subscribers_OfDifferentRooms_DoNotSeeEachOther()
    {
        var bus = new NotificationEventBus();
        var roomA = Guid.NewGuid();
        var roomB = Guid.NewGuid();
        var a = new List<CaseRoomSyncEvent>();
        var b = new List<CaseRoomSyncEvent>();

        using var subA = bus.SubscribeCaseRoomSync(roomA, a.Add);
        using var subB = bus.SubscribeCaseRoomSync(roomB, b.Add);

        bus.PublishCaseRoomSync(Evt(roomA, "Alice"));

        a.Should().ContainSingle().Which.DisplayName.Should().Be("Alice");
        b.Should().BeEmpty();
    }

    [Fact]
    public void Unsubscribe_StopsReceivingEvents()
    {
        var bus = new NotificationEventBus();
        var requestId = Guid.NewGuid();
        var received = new List<CaseRoomSyncEvent>();

        var sub = bus.SubscribeCaseRoomSync(requestId, received.Add);
        sub.Dispose();

        bus.PublishCaseRoomSync(Evt(requestId, "X"));

        received.Should().BeEmpty();
    }
}
