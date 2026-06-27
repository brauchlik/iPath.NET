### Task 2: Extend `INotificationEventBus` with CaseRoom channel

**Files:**
- Modify: `src/core/iPath.Application/Features/Notifications/NotificationEventBus.cs`
- Test: `test/iPath.Test.xUnit2/CaseRoom/CaseRoomEventBusTests.cs`

**Interfaces:**
- Consumes: `CaseRoomSyncEvent` from Task 1
- Produces: `INotificationEventBus.PublishCaseRoomSync(CaseRoomSyncEvent)` and `SubscribeCaseRoomSync(Action<CaseRoomSyncEvent>)`

- [ ] **Step 1: Write the failing test**

Create `test/iPath.Test.xUnit2/CaseRoom/CaseRoomEventBusTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomEventBusTests"`
Expected: Compile failure — `PublishCaseRoomSync` / `SubscribeCaseRoomSync` don't exist.

- [ ] **Step 3: Extend the interface and implementation**

Modify `src/core/iPath.Application/Features/Notifications/NotificationEventBus.cs`. Replace the interface and class definition with the version adding CaseRoom channel. Add `using iPath.Application.Features.CaseRoom;` at the top, and add these two methods inside `INotificationEventBus`:

```csharp
void PublishCaseRoomSync(CaseRoomSyncEvent evt);
IDisposable SubscribeCaseRoomSync(Action<CaseRoomSyncEvent> handler);
```

Inside `NotificationEventBus`, add a new `ConcurrentDictionary<Guid, Action<CaseRoomSyncEvent>> _caseRoomSubs = new();` and implementations mirroring `PublishSystemEvent` / `SubscribeSystemEvents`:

```csharp
public void PublishCaseRoomSync(CaseRoomSyncEvent evt)
{
    foreach (var h in _caseRoomSubs.Values.ToArray())
        h(evt);
}

public IDisposable SubscribeCaseRoomSync(Action<CaseRoomSyncEvent> handler)
{
    var key = Guid.NewGuid();
    _caseRoomSubs[key] = handler;
    return new Unsubscriber(() => _caseRoomSubs.TryRemove(key, out _));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomEventBusTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/core/iPath.Application/Features/Notifications/NotificationEventBus.cs test/iPath.Test.xUnit2/CaseRoom/CaseRoomEventBusTests.cs
git commit -m "feat(caseroom): extend NotificationEventBus with CaseRoom sync channel"
```

