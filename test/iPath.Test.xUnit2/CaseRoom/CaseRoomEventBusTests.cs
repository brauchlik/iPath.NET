using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class CaseRoomEventBusTests
{
    [Fact]
    public void SubscribeCaseRoomSync_ReceivesPublishedEvents()
    {
        var bus = new NotificationEventBus();
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var received = new List<CaseRoomSyncEvent>();

        var sub = bus.SubscribeCaseRoomSync(evt =>
        {
            if (evt.RequestId == requestId) received.Add(evt);
        });

        var evt1 = new CaseRoomSyncEvent(requestId, userId, "Alice",
            new SyncPayload(null, new ViewportState(0.1, 0.2, 0.3)), DateTimeOffset.UtcNow);
        var evt2 = new CaseRoomSyncEvent(Guid.NewGuid(), userId, "Bob",
            new SyncPayload(null, new ViewportState(1, 1, 1)), DateTimeOffset.UtcNow);

        bus.PublishCaseRoomSync(evt1);
        bus.PublishCaseRoomSync(evt2);

        received.Should().ContainSingle();
        received[0].DisplayName.Should().Be("Alice");
        sub.Dispose();
    }

    [Fact]
    public void Unsubscribe_StopsReceivingEvents()
    {
        var bus = new NotificationEventBus();
        var received = new List<CaseRoomSyncEvent>();

        var sub = bus.SubscribeCaseRoomSync(received.Add);
        sub.Dispose();

        bus.PublishCaseRoomSync(new CaseRoomSyncEvent(
            Guid.NewGuid(), Guid.NewGuid(), "X",
            new SyncPayload(null, null), DateTimeOffset.UtcNow));

        received.Should().BeEmpty();
    }
}
