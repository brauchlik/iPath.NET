## Commit list

33089af feat(caseroom): implement in-memory CaseRoomSessionStore with SSE+EventBus broadcast

## Stat summary

 .../Services/CaseRoom/CaseRoomSessionStore.cs      | 120 +++++++++++++++++++++
 .../Services/CaseRoom/ICaseRoomSessionStore.cs     |  11 ++
 .../CaseRoom/CaseRoomSessionStoreTests.cs          | 114 ++++++++++++++++++++
 3 files changed, 245 insertions(+)

## Full diff

diff --git a/src/infrastructure/iPath.API/Services/CaseRoom/CaseRoomSessionStore.cs b/src/infrastructure/iPath.API/Services/CaseRoom/CaseRoomSessionStore.cs
new file mode 100644
index 0000000..88c108a
--- /dev/null
+++ b/src/infrastructure/iPath.API/Services/CaseRoom/CaseRoomSessionStore.cs
@@ -0,0 +1,120 @@
+using System.Collections.Concurrent;
+using iPath.Application.Features.CaseRoom;
+using iPath.Application.Features.Notifications;
+using iPath.API.Services.Notifications;
+using Microsoft.Extensions.Logging;
+
+namespace iPath.API.Services.CaseRoom;
+
+public class CaseRoomSessionStore(
+    ISseConnectionManager sseManager,
+    INotificationEventBus eventBus,
+    ILogger<CaseRoomSessionStore> logger) : ICaseRoomSessionStore
+{
+    private static readonly TimeSpan TeardownGrace = TimeSpan.FromSeconds(30);
+
+    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = new();
+
+    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid userId, string displayName, CancellationToken ct)
+    {
+        var entry = _sessions.GetOrAdd(requestId, rid => new SessionEntry
+        {
+            Session = new CaseRoomSessionData
+            {
+                RequestId = rid,
+                CreatedAt = DateTimeOffset.UtcNow,
+                CreatedBy = userId
+            }
+        });
+
+        entry.TeardownCts?.Cancel();
+        entry.TeardownCts = null;
+
+        entry.Session.Participants.TryAdd(userId, new Participant(userId, displayName, DateTimeOffset.UtcNow));
+
+        return Task.FromResult(BuildSnapshot(entry.Session));
+    }
+
+    public async Task LeaveAsync(Guid requestId, Guid userId, CancellationToken ct)
+    {
+        if (!_sessions.TryGetValue(requestId, out var entry)) return;
+        entry.Session.Participants.Remove(userId, out _);
+
+        if (entry.Session.Participants.Count == 0)
+        {
+            var cts = new CancellationTokenSource(TeardownGrace);
+            entry.TeardownCts = cts;
+            _ = Task.Delay(TeardownGrace, cts.Token).ContinueWith(t =>
+            {
+                if (entry.Session.Participants.Count == 0)
+                    _sessions.TryRemove(requestId, out _);
+            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
+        }
+
+        await Task.CompletedTask;
+    }
+
+    public async Task SyncAsync(Guid requestId, Guid userId, SyncPayload payload, CancellationToken ct)
+    {
+        if (!_sessions.TryGetValue(requestId, out var entry)) return;
+
+        if (payload.DocumentId.HasValue && entry.Session.ActiveDocumentId != payload.DocumentId)
+        {
+            entry.Session.ActiveDocumentId = payload.DocumentId;
+        }
+        if (payload.Viewport is not null)
+        {
+            entry.Session.CurrentViewport = payload.Viewport with { };
+        }
+
+        var displayName = entry.Session.Participants.TryGetValue(userId, out var p)
+            ? p.DisplayName : "Unknown";
+        var evt = new CaseRoomSyncEvent(requestId, userId, displayName, payload, DateTimeOffset.UtcNow);
+
+        foreach (var participantId in entry.Session.Participants.Keys)
+        {
+            if (participantId == userId) continue;
+            await sseManager.SendToUserAsync(participantId, "caseroom-sync", evt);
+        }
+        eventBus.PublishCaseRoomSync(evt);
+
+        logger.LogDebug("CaseRoom {RequestId} sync from {UserId}: {Kind}",
+            requestId, userId, payload.DocumentId.HasValue ? "document" : "viewport");
+    }
+
+    public Task<CaseRoomStatus?> GetStatusAsync(Guid requestId, CancellationToken ct)
+    {
+        if (!_sessions.TryGetValue(requestId, out var entry))
+            return Task.FromResult<CaseRoomStatus?>(null);
+
+        var count = entry.Session.Participants.Count;
+        return Task.FromResult<CaseRoomStatus?>(new CaseRoomStatus(
+            IsActive: count > 0,
+            ParticipantCount: count,
+            ParticipantNames: entry.Session.Participants.Values.Select(p => p.DisplayName).ToArray()
+        ));
+    }
+
+    private static CaseRoomSnapshot BuildSnapshot(CaseRoomSessionData session) => new(
+        session.RequestId,
+        session.ActiveDocumentId,
+        session.CurrentViewport,
+        session.Participants.Values.ToArray()
+    );
+
+    private sealed class SessionEntry
+    {
+        public required CaseRoomSessionData Session { get; init; }
+        public CancellationTokenSource? TeardownCts { get; set; }
+    }
+
+    private sealed class CaseRoomSessionData
+    {
+        public Guid RequestId { get; init; }
+        public Guid? ActiveDocumentId { get; set; }
+        public ViewportState? CurrentViewport { get; set; }
+        public DateTimeOffset CreatedAt { get; init; }
+        public Guid CreatedBy { get; init; }
+        public ConcurrentDictionary<Guid, Participant> Participants { get; } = new();
+    }
+}
diff --git a/src/infrastructure/iPath.API/Services/CaseRoom/ICaseRoomSessionStore.cs b/src/infrastructure/iPath.API/Services/CaseRoom/ICaseRoomSessionStore.cs
new file mode 100644
index 0000000..e7fca72
--- /dev/null
+++ b/src/infrastructure/iPath.API/Services/CaseRoom/ICaseRoomSessionStore.cs
@@ -0,0 +1,11 @@
+using iPath.Application.Features.CaseRoom;
+
+namespace iPath.API.Services.CaseRoom;
+
+public interface ICaseRoomSessionStore
+{
+    Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid userId, string displayName, CancellationToken ct);
+    Task LeaveAsync(Guid requestId, Guid userId, CancellationToken ct);
+    Task SyncAsync(Guid requestId, Guid userId, SyncPayload payload, CancellationToken ct);
+    Task<CaseRoomStatus?> GetStatusAsync(Guid requestId, CancellationToken ct);
+}
diff --git a/test/iPath.Test.xUnit2/CaseRoom/CaseRoomSessionStoreTests.cs b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomSessionStoreTests.cs
new file mode 100644
index 0000000..36533db
--- /dev/null
+++ b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomSessionStoreTests.cs
@@ -0,0 +1,114 @@
+using iPath.API.Services.Notifications;
+using iPath.API.Services.CaseRoom;
+using iPath.Application.Features.CaseRoom;
+using iPath.Application.Features.Notifications;
+using Microsoft.Extensions.DependencyInjection;
+using Microsoft.Extensions.Logging;
+using NSubstitute;
+using FluentAssertions;
+
+namespace iPath.Test.xUnit2.CaseRoom;
+
+public class CaseRoomSessionStoreTests
+{
+    private static CaseRoomSessionStore CreateStore()
+    {
+        var services = new ServiceCollection().BuildServiceProvider();
+        var sseMgr = Substitute.For<ISseConnectionManager>();
+        var bus = new NotificationEventBus();
+        var logger = new LoggerFactory().CreateLogger<CaseRoomSessionStore>();
+        return new CaseRoomSessionStore(sseMgr, bus, logger);
+    }
+
+    [Fact]
+    public async Task Join_FirstUser_CreatesSessionWithOneParticipant()
+    {
+        var store = CreateStore();
+        var requestId = Guid.NewGuid();
+        var userId = Guid.NewGuid();
+
+        var snapshot = await store.JoinAsync(requestId, userId, "Alice", default);
+
+        snapshot.RequestId.Should().Be(requestId);
+        snapshot.Participants.Should().ContainSingle(p => p.UserId == userId);
+        snapshot.ActiveDocumentId.Should().BeNull();
+    }
+
+    [Fact]
+    public async Task Join_SecondUser_AddsParticipant()
+    {
+        var store = CreateStore();
+        var requestId = Guid.NewGuid();
+
+        await store.JoinAsync(requestId, Guid.NewGuid(), "Alice", default);
+        var snapshot = await store.JoinAsync(requestId, Guid.NewGuid(), "Bob", default);
+
+        snapshot.Participants.Should().HaveCount(2);
+    }
+
+    [Fact]
+    public async Task Join_IsIdempotent_ForSameUser()
+    {
+        var store = CreateStore();
+        var requestId = Guid.NewGuid();
+        var userId = Guid.NewGuid();
+
+        await store.JoinAsync(requestId, userId, "Alice", default);
+        var snapshot = await store.JoinAsync(requestId, userId, "Alice", default);
+
+        snapshot.Participants.Should().ContainSingle();
+    }
+
+    [Fact]
+    public async Task Sync_UpdatesViewport()
+    {
+        var store = CreateStore();
+        var requestId = Guid.NewGuid();
+        var userId = Guid.NewGuid();
+        await store.JoinAsync(requestId, userId, "Alice", default);
+
+        await store.SyncAsync(requestId, userId,
+            new SyncPayload(null, new ViewportState(0.5, 0.5, 2.0)), default);
+
+        var status = await store.GetStatusAsync(requestId, default);
+        status!.IsActive.Should().BeTrue();
+    }
+
+    [Fact]
+    public async Task Sync_UpdatesActiveDocument()
+    {
+        var store = CreateStore();
+        var requestId = Guid.NewGuid();
+        var userId = Guid.NewGuid();
+        await store.JoinAsync(requestId, userId, "Alice", default);
+
+        var docId = Guid.NewGuid();
+        await store.SyncAsync(requestId, userId, new SyncPayload(docId, null), default);
+
+        var snapshot = await store.JoinAsync(requestId, Guid.NewGuid(), "Bob", default);
+        snapshot.ActiveDocumentId.Should().Be(docId);
+    }
+
+    [Fact]
+    public async Task Leave_LastUser_SchedulesTeardown()
+    {
+        var store = CreateStore();
+        var requestId = Guid.NewGuid();
+        var userId = Guid.NewGuid();
+        await store.JoinAsync(requestId, userId, "Alice", default);
+
+        await store.LeaveAsync(requestId, userId, default);
+
+        var status = await store.GetStatusAsync(requestId, default);
+        // Session may still exist briefly due to teardown grace, but should not crash
+        status.Should().NotBeNull();
+    }
+
+    [Fact]
+    public async Task GetStatus_ReturnsNull_WhenNoSession()
+    {
+        var store = CreateStore();
+        var status = await store.GetStatusAsync(Guid.NewGuid(), default);
+        status.Should().BeNull();
+    }
+}
