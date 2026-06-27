## Commit list

3400c48 feat(caseroom): add domain models and sync service contracts

## Stat summary

 .../Features/CaseRoom/CaseRoomModels.cs            | 22 +++++++++++
 .../Features/CaseRoom/ICaseRoomSyncService.cs      | 13 ++++++
 .../CaseRoom/CaseRoomModelsTests.cs                | 46 ++++++++++++++++++++++
 3 files changed, 81 insertions(+)

## Full diff

diff --git a/src/core/iPath.Application/Features/CaseRoom/CaseRoomModels.cs b/src/core/iPath.Application/Features/CaseRoom/CaseRoomModels.cs
new file mode 100644
index 0000000..bc3fcea
--- /dev/null
+++ b/src/core/iPath.Application/Features/CaseRoom/CaseRoomModels.cs
@@ -0,0 +1,22 @@
+namespace iPath.Application.Features.CaseRoom;
+
+public record ViewportState(double X, double Y, double Zoom);
+
+public record Participant(Guid UserId, string DisplayName, DateTimeOffset JoinedAt);
+
+public record SyncPayload(Guid? DocumentId, ViewportState? Viewport);
+
+public record CaseRoomSnapshot(
+    Guid RequestId,
+    Guid? ActiveDocumentId,
+    ViewportState? Viewport,
+    Participant[] Participants);
+
+public record CaseRoomStatus(bool IsActive, int ParticipantCount, string[] ParticipantNames);
+
+public record CaseRoomSyncEvent(
+    Guid RequestId,
+    Guid UserId,
+    string DisplayName,
+    SyncPayload Payload,
+    DateTimeOffset Timestamp);
\ No newline at end of file
diff --git a/src/core/iPath.Application/Features/CaseRoom/ICaseRoomSyncService.cs b/src/core/iPath.Application/Features/CaseRoom/ICaseRoomSyncService.cs
new file mode 100644
index 0000000..e5dff23
--- /dev/null
+++ b/src/core/iPath.Application/Features/CaseRoom/ICaseRoomSyncService.cs
@@ -0,0 +1,13 @@
+namespace iPath.Application.Features.CaseRoom;
+
+public interface ICaseRoomSyncService : IAsyncDisposable
+{
+    Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default);
+    Task LeaveAsync(Guid requestId, CancellationToken ct = default);
+    Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default);
+}
+
+public interface ICaseRoomSyncReceiver
+{
+    IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler);
+}
\ No newline at end of file
diff --git a/test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs
new file mode 100644
index 0000000..b4420c2
--- /dev/null
+++ b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs
@@ -0,0 +1,46 @@
+using iPath.Application.Features.CaseRoom;
+using FluentAssertions;
+
+namespace iPath.Test.xUnit2.CaseRoom;
+
+public class CaseRoomModelsTests
+{
+    [Fact]
+    public void ViewportState_ConstructsWithXYZ()
+    {
+        var v = new ViewportState(1.5, 2.5, 3.5);
+        v.X.Should().Be(1.5);
+        v.Y.Should().Be(2.5);
+        v.Zoom.Should().Be(3.5);
+    }
+
+    [Fact]
+    public void SyncPayload_AllowsDocumentOnly()
+    {
+        var p = new SyncPayload(DocumentId: Guid.NewGuid(), Viewport: null);
+        p.DocumentId.Should().NotBeNull();
+        p.Viewport.Should().BeNull();
+    }
+
+    [Fact]
+    public void SyncPayload_AllowsViewportOnly()
+    {
+        var p = new SyncPayload(DocumentId: null, Viewport: new ViewportState(1, 2, 3));
+        p.DocumentId.Should().BeNull();
+        p.Viewport.Should().NotBeNull();
+    }
+
+    [Fact]
+    public void CaseRoomSyncEvent_HasRequestIdUserIdAndPayload()
+    {
+        var requestId = Guid.NewGuid();
+        var userId = Guid.NewGuid();
+        var payload = new SyncPayload(null, new ViewportState(0.5, 0.5, 1.0));
+        var evt = new CaseRoomSyncEvent(requestId, userId, "Alice", payload, DateTimeOffset.UtcNow);
+
+        evt.RequestId.Should().Be(requestId);
+        evt.UserId.Should().Be(userId);
+        evt.DisplayName.Should().Be("Alice");
+        evt.Payload.Viewport!.Zoom.Should().Be(1.0);
+    }
+}
\ No newline at end of file
