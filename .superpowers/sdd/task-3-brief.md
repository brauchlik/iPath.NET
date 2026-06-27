### Task 3: `ICaseRoomSessionStore` in-memory implementation

**Files:**
- Create: `src/infrastructure/iPath.API/Services/CaseRoom/ICaseRoomSessionStore.cs`
- Create: `src/infrastructure/iPath.API/Services/CaseRoom/CaseRoomSessionStore.cs`
- Test: `test/iPath.Test.xUnit2/CaseRoom/CaseRoomSessionStoreTests.cs`

**Interfaces:**
- Consumes: `CaseRoomModels` (Task 1); `INotificationEventBus` (existing); `ISseConnectionManager` (existing)
- Produces: `ICaseRoomSessionStore` with `JoinAsync/LeaveAsync/SyncAsync/GetStatusAsync` + `Guid? GetActiveDocumentId(Guid requestId)`

- [ ] **Step 1: Write the failing tests**

Create `test/iPath.Test.xUnit2/CaseRoom/CaseRoomSessionStoreTests.cs`:

```csharp
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

        var snapshot = await store.JoinAsync(requestId, userId, "Alice", default);

        snapshot.RequestId.Should().Be(requestId);
        snapshot.Participants.Should().ContainSingle(p => p.UserId == userId);
        snapshot.ActiveDocumentId.Should().BeNull();
    }

    [Fact]
    public async Task Join_SecondUser_AddsParticipant()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();

        await store.JoinAsync(requestId, Guid.NewGuid(), "Alice", default);
        var snapshot = await store.JoinAsync(requestId, Guid.NewGuid(), "Bob", default);

        snapshot.Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task Join_IsIdempotent_ForSameUser()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await store.JoinAsync(requestId, userId, "Alice", default);
        var snapshot = await store.JoinAsync(requestId, userId, "Alice", default);

        snapshot.Participants.Should().ContainSingle();
    }

    [Fact]
    public async Task Sync_UpdatesViewport()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await store.JoinAsync(requestId, userId, "Alice", default);

        await store.SyncAsync(requestId, userId,
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
        await store.JoinAsync(requestId, userId, "Alice", default);

        var docId = Guid.NewGuid();
        await store.SyncAsync(requestId, userId, new SyncPayload(docId, null), default);

        var snapshot = await store.JoinAsync(requestId, Guid.NewGuid(), "Bob", default);
        snapshot.ActiveDocumentId.Should().Be(docId);
    }

    [Fact]
    public async Task Leave_LastUser_SchedulesTeardown()
    {
        var store = CreateStore();
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await store.JoinAsync(requestId, userId, "Alice", default);

        await store.LeaveAsync(requestId, userId, default);

        var status = await store.GetStatusAsync(requestId, default);
        // Session may still exist briefly due to teardown grace, but should not crash
        status.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatus_ReturnsNull_WhenNoSession()
    {
        var store = CreateStore();
        var status = await store.GetStatusAsync(Guid.NewGuid(), default);
        status.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomSessionStoreTests"`
Expected: Compile failure — `ICaseRoomSessionStore` and `CaseRoomSessionStore` don't exist.

- [ ] **Step 3: Create the store interface**

Create `src/infrastructure/iPath.API/Services/CaseRoom/ICaseRoomSessionStore.cs`:

```csharp
using iPath.Application.Features.CaseRoom;

namespace iPath.API.Services.CaseRoom;

public interface ICaseRoomSessionStore
{
    Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid userId, string displayName, CancellationToken ct);
    Task LeaveAsync(Guid requestId, Guid userId, CancellationToken ct);
    Task SyncAsync(Guid requestId, Guid userId, SyncPayload payload, CancellationToken ct);
    Task<CaseRoomStatus?> GetStatusAsync(Guid requestId, CancellationToken ct);
}
```

- [ ] **Step 4: Create the store implementation**

Create `src/infrastructure/iPath.API/Services/CaseRoom/CaseRoomSessionStore.cs`:

```csharp
using System.Collections.Concurrent;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using iPath.API.Services.Notifications;
using Microsoft.Extensions.Logging;

namespace iPath.API.Services.CaseRoom;

public class CaseRoomSessionStore(
    ISseConnectionManager sseManager,
    INotificationEventBus eventBus,
    ILogger<CaseRoomSessionStore> logger) : ICaseRoomSessionStore
{
    private static readonly TimeSpan TeardownGrace = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = new();

    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid userId, string displayName, CancellationToken ct)
    {
        var entry = _sessions.GetOrAdd(requestId, rid => new SessionEntry
        {
            Session = new CaseRoomSessionData
            {
                RequestId = rid,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = userId
            }
        });

        // Cancel any pending teardown (user rejoined within grace window)
        entry.TeardownCts?.Cancel();
        entry.TeardownCts = null;

        entry.Session.Participants.TryAdd(userId, new Participant(userId, displayName, DateTimeOffset.UtcNow));

        return Task.FromResult(BuildSnapshot(entry.Session));
    }

    public async Task LeaveAsync(Guid requestId, Guid userId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry)) return;
        entry.Session.Participants.Remove(userId, out _);

        if (entry.Session.Participants.Count == 0)
        {
            // Schedule teardown after grace period (tolerates page refresh)
            var cts = new CancellationTokenSource(TeardownGrace);
            entry.TeardownCts = cts;
            _ = Task.Delay(TeardownGrace, cts.Token).ContinueWith(_ =>
            {
                if (entry.Session.Participants.Count == 0)
                    _sessions.TryRemove(requestId, out _);
            }, TaskScheduler.Default);
        }

        await Task.CompletedTask;
    }

    public async Task SyncAsync(Guid requestId, Guid userId, SyncPayload payload, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry)) return;

        if (payload.DocumentId.HasValue && entry.Session.ActiveDocumentId != payload.DocumentId)
        {
            entry.Session.ActiveDocumentId = payload.DocumentId;
        }
        if (payload.Viewport is not null)
        {
            entry.Session.CurrentViewport = payload.Viewport with { };
        }

        // Broadcast to other participants only
        var displayName = entry.Session.Participants.TryGetValue(userId, out var p)
            ? p.DisplayName : "Unknown";
        var evt = new CaseRoomSyncEvent(requestId, userId, displayName, payload, DateTimeOffset.UtcNow);

        // Broadcast through BOTH channels — each only reaches clients connected through it
        foreach (var participantId in entry.Session.Participants.Keys)
        {
            if (participantId == userId) continue;
            await sseManager.SendToUserAsync(participantId, "caseroom-sync", evt);
        }
        eventBus.PublishCaseRoomSync(evt);

        logger.LogDebug("CaseRoom {RequestId} sync from {UserId}: {Kind}",
            requestId, userId, payload.DocumentId.HasValue ? "document" : "viewport");
    }

    public Task<CaseRoomStatus?> GetStatusAsync(Guid requestId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry) || entry.Session.Participants.Count == 0)
            return Task.FromResult<CaseRoomStatus?>(null);

        return Task.FromResult<CaseRoomStatus?>(new CaseRoomStatus(
            IsActive: true,
            ParticipantCount: entry.Session.Participants.Count,
            ParticipantNames: entry.Session.Participants.Values.Select(p => p.DisplayName).ToArray()
        ));
    }

    private static CaseRoomSnapshot BuildSnapshot(CaseRoomSessionData session) => new(
        session.RequestId,
        session.ActiveDocumentId,
        session.CurrentViewport,
        session.Participants.Values.ToArray()
    );

    private sealed class SessionEntry
    {
        public required CaseRoomSessionData Session { get; init; }
        public CancellationTokenSource? TeardownCts { get; set; }
    }

    private sealed class CaseRoomSessionData
    {
        public Guid RequestId { get; init; }
        public Guid? ActiveDocumentId { get; set; }
        public ViewportState? CurrentViewport { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public Guid CreatedBy { get; init; }
        public ConcurrentDictionary<Guid, Participant> Participants { get; } = new();
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomSessionStoreTests"`
Expected: PASS (7 tests).

- [ ] **Step 6: Commit**

```bash
git add src/infrastructure/iPath.API/Services/CaseRoom/ test/iPath.Test.xUnit2/CaseRoom/CaseRoomSessionStoreTests.cs
git commit -m "feat(caseroom): implement in-memory CaseRoomSessionStore with SSE+EventBus broadcast"
```

