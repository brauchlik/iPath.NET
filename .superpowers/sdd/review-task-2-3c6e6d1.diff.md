## Commit list

3c6e6d1 feat(caseroom): extend NotificationEventBus with CaseRoom sync channel

## Stat summary

 .../Features/Notifications/NotificationEventBus.cs | 18 ++++++++
 .../CaseRoom/CaseRoomEventBusTests.cs              | 50 ++++++++++++++++++++++
 2 files changed, 68 insertions(+)

## Full diff

diff --git a/src/core/iPath.Application/Features/Notifications/NotificationEventBus.cs b/src/core/iPath.Application/Features/Notifications/NotificationEventBus.cs
index 1a42e85..70d746e 100644
--- a/src/core/iPath.Application/Features/Notifications/NotificationEventBus.cs
+++ b/src/core/iPath.Application/Features/Notifications/NotificationEventBus.cs
@@ -1,31 +1,36 @@
 using System.Collections.Concurrent;
+using iPath.Application.Features.CaseRoom;
 
 namespace iPath.Application.Features.Notifications;
 
 public interface INotificationEventBus
 {
     void PublishNotification(Guid userId, NotificationDto dto);
     IDisposable SubscribeNotifications(Guid userId, Action<NotificationDto> handler);
 
     void PublishDomainEvent(DomainEventSummary evt);
     IDisposable SubscribeDomainEvents(Action<DomainEventSummary> handler);
 
     void PublishSystemEvent(SystemEventHint hint);
     IDisposable SubscribeSystemEvents(Action<SystemEventHint> handler);
+
+    void PublishCaseRoomSync(CaseRoomSyncEvent evt);
+    IDisposable SubscribeCaseRoomSync(Action<CaseRoomSyncEvent> handler);
 }
 
 public class NotificationEventBus : INotificationEventBus
 {
     private readonly ConcurrentDictionary<Guid, List<Action<NotificationDto>>> _notificationSubs = new();
     private readonly ConcurrentDictionary<Guid, Action<DomainEventSummary>> _domainSubs = new();
     private readonly ConcurrentDictionary<Guid, Action<SystemEventHint>> _systemSubs = new();
+    private readonly ConcurrentDictionary<Guid, Action<CaseRoomSyncEvent>> _caseRoomSubs = new();
 
     public void PublishNotification(Guid userId, NotificationDto dto)
     {
         if (_notificationSubs.TryGetValue(userId, out var handlers))
         {
             foreach (var h in handlers.ToArray())
                 h(dto);
         }
     }
 
@@ -63,16 +68,29 @@ public class NotificationEventBus : INotificationEventBus
         foreach (var h in _systemSubs.Values.ToArray())
             h(hint);
     }
 
     public IDisposable SubscribeSystemEvents(Action<SystemEventHint> handler)
     {
         var key = Guid.NewGuid();
         _systemSubs[key] = handler;
         return new Unsubscriber(() => _systemSubs.TryRemove(key, out _));
     }
+
+    public void PublishCaseRoomSync(CaseRoomSyncEvent evt)
+    {
+        foreach (var h in _caseRoomSubs.Values.ToArray())
+            h(evt);
+    }
+
+    public IDisposable SubscribeCaseRoomSync(Action<CaseRoomSyncEvent> handler)
+    {
+        var key = Guid.NewGuid();
+        _caseRoomSubs[key] = handler;
+        return new Unsubscriber(() => _caseRoomSubs.TryRemove(key, out _));
+    }
 }
 
 file class Unsubscriber(Action dispose) : IDisposable
 {
     public void Dispose() => dispose();
 }
diff --git a/test/iPath.Test.xUnit2/CaseRoom/CaseRoomEventBusTests.cs b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomEventBusTests.cs
new file mode 100644
index 0000000..c3790a9
--- /dev/null
+++ b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomEventBusTests.cs
@@ -0,0 +1,50 @@
+using iPath.Application.Features.CaseRoom;
+using iPath.Application.Features.Notifications;
+using FluentAssertions;
+
+namespace iPath.Test.xUnit2.CaseRoom;
+
+public class CaseRoomEventBusTests
+{
+    [Fact]
+    public void SubscribeCaseRoomSync_ReceivesPublishedEvents()
+    {
+        var bus = new NotificationEventBus();
+        var requestId = Guid.NewGuid();
+        var userId = Guid.NewGuid();
+        var received = new List<CaseRoomSyncEvent>();
+
+        var sub = bus.SubscribeCaseRoomSync(evt =>
+        {
+            if (evt.RequestId == requestId) received.Add(evt);
+        });
+
+        var evt1 = new CaseRoomSyncEvent(requestId, userId, "Alice",
+            new SyncPayload(null, new ViewportState(0.1, 0.2, 0.3)), DateTimeOffset.UtcNow);
+        var evt2 = new CaseRoomSyncEvent(Guid.NewGuid(), userId, "Bob",
+            new SyncPayload(null, new ViewportState(1, 1, 1)), DateTimeOffset.UtcNow);
+
+        bus.PublishCaseRoomSync(evt1);
+        bus.PublishCaseRoomSync(evt2);
+
+        received.Should().ContainSingle();
+        received[0].DisplayName.Should().Be("Alice");
+        sub.Dispose();
+    }
+
+    [Fact]
+    public void Unsubscribe_StopsReceivingEvents()
+    {
+        var bus = new NotificationEventBus();
+        var received = new List<CaseRoomSyncEvent>();
+
+        var sub = bus.SubscribeCaseRoomSync(received.Add);
+        sub.Dispose();
+
+        bus.PublishCaseRoomSync(new CaseRoomSyncEvent(
+            Guid.NewGuid(), Guid.NewGuid(), "X",
+            new SyncPayload(null, null), DateTimeOffset.UtcNow));
+
+        received.Should().BeEmpty();
+    }
+}
