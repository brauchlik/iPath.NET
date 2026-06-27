using iPath.Application.Features.CaseRoom;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class CaseRoomModelsTests
{
    [Fact]
    public void ViewportState_ConstructsWithXYZ()
    {
        var v = new ViewportState(1.5, 2.5, 3.5);
        v.X.Should().Be(1.5);
        v.Y.Should().Be(2.5);
        v.Zoom.Should().Be(3.5);
    }

    [Fact]
    public void SyncPayload_AllowsDocumentOnly()
    {
        var p = new SyncPayload(DocumentId: Guid.NewGuid(), Viewport: null);
        p.DocumentId.Should().NotBeNull();
        p.Viewport.Should().BeNull();
    }

    [Fact]
    public void SyncPayload_AllowsViewportOnly()
    {
        var p = new SyncPayload(DocumentId: null, Viewport: new ViewportState(1, 2, 3));
        p.DocumentId.Should().BeNull();
        p.Viewport.Should().NotBeNull();
    }

    [Fact]
    public void CaseRoomSyncEvent_HasRequestIdUserIdAndPayload()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var payload = new SyncPayload(null, new ViewportState(0.5, 0.5, 1.0));
        var evt = new CaseRoomSyncEvent(requestId, userId, "Alice", payload, DateTimeOffset.UtcNow);

        evt.RequestId.Should().Be(requestId);
        evt.UserId.Should().Be(userId);
        evt.DisplayName.Should().Be("Alice");
        evt.Payload.Viewport!.Zoom.Should().Be(1.0);
    }
}