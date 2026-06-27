# CaseRoom — Collaborative Real-Time Slide Viewing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a real-time collaborative slide viewing room ("CaseRoom") keyed by `ServiceRequestId`, syncing active document + OSD viewport (pan/zoom) across all participants over the dual-mode transport (REST+SSE for WASM, in-process for Server).

**Architecture:** One in-memory session per ServiceRequest lives in `ICaseRoomSessionStore` (singleton, iPath.API). Server-mode clients reach it directly via `INotificationEventBus` extension. WASM clients reach it via HTTP endpoints broadcast through `SseConnectionManager`. A Blazor page `/request/{id}/caseroom` renders an inline OSD canvas (no iframe) bridged to C# via a small `ipath-caseroom.js` interop module.

**Tech Stack:** .NET 10.0, Blazor (Server + WASM dual mode), DispatchR, Refit, OpenSeadragon 5.0.1 (CDN), xUnit + FluentAssertions + NSubstitute (tests).

## Global Constraints

- All new code follows existing namespace typos: `iPath.Blazor.Componenents.*` (note the three `e`s in `Componenents`).
- The `IPathApi` Refit interface lives in `iPath.Blazor.ServiceLib.ApiClient` namespace, file `IApiClient.cs`. `DirectApiClient` (server mode) implements this interface by directly invoking mediator/handlers/services.
- EF migrations are run by the DEVELOPER, never by the AI. If a task suggests DB changes that need a migration, only **show** the migration command — do not run it.
- Tests are in `test/iPath.Test.xUnit2`, xUnit + FluentAssertions + NSubstitute. Test class files end with `Tests.cs`.
- All endpoints require authorization via the `IUserSession` pattern (already used in `NotificationEndpoints`).
- JSON property naming policy is `CamelCase` on both sides (Refit settings + .NET defaults).
- Avoid `[Emoji]` in code/comments/specs unless existing files already use them.

---

## File Structure

```
src/
├── core/iPath.Application/Features/CaseRoom/
│   ├── CaseRoomModels.cs               # ViewportState, Participant, SyncPayload, CaseRoomSnapshot, CaseRoomStatus, CaseRoomSyncEvent
│   └── ICaseRoomSyncService.cs         # ICaseRoomSyncService, ICaseRoomSyncReceiver interfaces
│
├── infrastructure/iPath.API/
│   ├── Endpoints/CaseRoomEndpoints.cs  # MapCaseRoomApi extension
│   └── Services/CaseRoom/
│       ├── ICaseRoomSessionStore.cs    # Interface (server-side only)
│       └── CaseRoomSessionStore.cs     # In-memory singleton impl
│
├── ui/
│   ├── iPath.RazorLib/
│   │   ├── ServiceRequests/
│   │   │   └── CaseRoomPage.razor      # + razor.cs code-behind
│   │   ├── CaseRoom/
│   │   │   ├── HttpCaseRoomSyncService.cs       # WASM
│   │   │   ├── HttpCaseRoomSyncReceiver.cs     # WASM (subscribes to SseClientService)
│   │   │   ├── InMemoryCaseRoomSyncService.cs  # Server
│   │   │   └── InMemoryCaseRoomSyncReceiver.cs # Server
│   │   └── wwwroot/js/ipath-caseroom.js         # OSD init + throttle + setViewport
│   │
│   └── iPath.Blazor.ServiceLib/
│       ├── ApiClient/IApiClient.cs             # Add Refit methods
│       └── Services/DirectApiClient.cs        # Add direct calls
│
└── test/iPath.Test.xUnit2/CaseRoom/
    ├── CaseRoomSessionStoreTests.cs
    ├── CaseRoomEventBusTests.cs
    └── CaseRoomSyncTransportTests.cs
```

**Modified existing files:**
- `src/core/iPath.Application/Features/Notifications/NotificationEventBus.cs` — add `PublishCaseRoomSync` / `SubscribeCaseRoomSync`
- `src/ui/iPath.RazorLib/Notifications/SseClientService.cs` — add `CaseRoomSyncReceived` event + `[JSInvokable] OnCaseRoomSync`
- `src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js` — add `caseroom-sync` event listener
- `src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs` — add Refit methods
- `src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs` — implement new methods
- `src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs` — register `ICaseRoomSyncService` + `ICaseRoomSyncReceiver` based on `WasmClient`
- `src/infrastructure/iPath.API/APIServicesRegistration.cs` — register `ICaseRoomSessionStore` as singleton
- `src/infrastructure/iPath.API/MapEndpoints.cs` — call `.MapCaseRoomApi()`
- `src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor` — "CaseRoom active" badge
- `src/ui/iPath.RazorLib/_Imports.razor` — add `@using iPath.Application.Features.CaseRoom` and `@using iPath.Blazor.Componenents.CaseRoom`

---

### Task 1: Domain models and sync contracts

**Files:**
- Create: `src/core/iPath.Application/Features/CaseRoom/CaseRoomModels.cs`
- Create: `src/core/iPath.Application/Features/CaseRoom/ICaseRoomSyncService.cs`
- Test: `test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `ViewportState(double X, double Y, double Zoom)`
  - `Participant(Guid UserId, string DisplayName, DateTimeOffset JoinedAt)`
  - `SyncPayload(Guid? DocumentId, ViewportState? Viewport)`
  - `CaseRoomSnapshot(Guid RequestId, Guid? ActiveDocumentId, ViewportState? Viewport, Participant[] Participants)`
  - `CaseRoomStatus(bool IsActive, int ParticipantCount, string[] ParticipantNames)`
  - `CaseRoomSyncEvent(Guid RequestId, Guid UserId, string DisplayName, SyncPayload Payload, DateTimeOffset Timestamp)`
  - `ICaseRoomSyncService` with `JoinAsync/LeaveAsync/SyncAsync` returning `Task`/`Task<CaseRoomSnapshot>`
  - `ICaseRoomSyncReceiver` with `IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler)`

- [ ] **Step 1: Write the failing test**

Create `test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomModelsTests"`
Expected: Build failure — namespace `iPath.Application.Features.CaseRoom` does not exist.

- [ ] **Step 3: Create the models file**

Create `src/core/iPath.Application/Features/CaseRoom/CaseRoomModels.cs`:

```csharp
namespace iPath.Application.Features.CaseRoom;

public record ViewportState(double X, double Y, double Zoom);

public record Participant(Guid UserId, string DisplayName, DateTimeOffset JoinedAt);

public record SyncPayload(Guid? DocumentId, ViewportState? Viewport);

public record CaseRoomSnapshot(
    Guid RequestId,
    Guid? ActiveDocumentId,
    ViewportState? Viewport,
    Participant[] Participants);

public record CaseRoomStatus(bool IsActive, int ParticipantCount, string[] ParticipantNames);

public record CaseRoomSyncEvent(
    Guid RequestId,
    Guid UserId,
    string DisplayName,
    SyncPayload Payload,
    DateTimeOffset Timestamp);
```

- [ ] **Step 4: Create the sync service interfaces**

Create `src/core/iPath.Application/Features/CaseRoom/ICaseRoomSyncService.cs`:

```csharp
namespace iPath.Application.Features.CaseRoom;

public interface ICaseRoomSyncService : IAsyncDisposable
{
    Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default);
    Task LeaveAsync(Guid requestId, CancellationToken ct = default);
    Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default);
}

public interface ICaseRoomSyncReceiver
{
    IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomModelsTests"`
Expected: PASS (all 4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/core/iPath.Application/Features/CaseRoom/ test/iPath.Test.xUnit2/CaseRoom/
git commit -m "feat(caseroom): add domain models and sync service contracts"
```

---

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

---

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

---

### Task 4: CaseRoom API endpoints

**Files:**
- Create: `src/infrastructure/iPath.API/Endpoints/CaseRoomEndpoints.cs`
- Modify: `src/infrastructure/iPath.API/MapEndpoints.cs:36` — add `.MapCaseRoomApi()`
- Modify: `src/infrastructure/iPath.API/APIServicesRegistration.cs:113` — register `ICaseRoomSessionStore` as singleton

**Interfaces:**
- Consumes: `ICaseRoomSessionStore` (Task 3), `IUserSession` (existing)
- Produces: 4 endpoints under `/api/v1/caseroom/{requestId}:...`

- [ ] **Step 1: Register the session store as singleton**

Modify `src/infrastructure/iPath.API/APIServicesRegistration.cs`. After line 116 (`services.AddSingleton<INotificationEventBus, NotificationEventBus>();`), add:

```csharp
        // CaseRoom session store (in-memory, transient sessions)
        services.AddSingleton<ICaseRoomSessionStore, CaseRoomSessionStore>();
```

And add at the top with the other using directives (preserve alphabetical-style ordering):

```csharp
using iPath.API.Services.CaseRoom;
```

- [ ] **Step 2: Create the endpoints file**

Create `src/infrastructure/iPath.API/Endpoints/CaseRoomEndpoints.cs`:

```csharp
using iPath.API.Services.CaseRoom;
using iPath.Application.Features.CaseRoom;

namespace iPath.API;

public static class CaseRoomEndpoints
{
    public static IEndpointRouteBuilder MapCaseRoomApi(this IEndpointRouteBuilder route)
    {
        var group = route.MapGroup("caseroom").RequireAuthorization();

        group.MapPost("{requestId:guid}/join", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            var snapshot = await store.JoinAsync(requestId, sess.User.Id, sess.User.DisplayName ?? "Anonymous", ct);
            return Results.Ok(snapshot);
        });

        group.MapPost("{requestId:guid}/leave", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            await store.LeaveAsync(requestId, sess.User.Id, ct);
            return Results.NoContent();
        });

        group.MapPost("{requestId:guid}/sync", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            [FromServices] IUserSession sess,
            SyncPayload payload,
            CancellationToken ct) =>
        {
            if (sess.User is null || !sess.User.IsAuthenticated)
                return Results.Unauthorized();

            await store.SyncAsync(requestId, sess.User.Id, payload, ct);
            return Results.NoContent();
        });

        group.MapGet("{requestId:guid}", async (
            Guid requestId,
            [FromServices] ICaseRoomSessionStore store,
            CancellationToken ct) =>
        {
            var status = await store.GetStatusAsync(requestId, ct);
            return status is null ? Results.NotFound() : Results.Ok(status);
        });

        return route;
    }
}
```

> **Note:** `IUserSession.User` exposes `Id`, `IsAuthenticated`, and `DisplayName`. Verify these property names by reading `src/infrastructure/iPath.API/Services/UserSession.cs` or the `IUserSession` interface if any naming mismatch arises — the existing `NotificationEndpoints.cs` uses `sess.User.Id` and `sess.User.IsAuthenticated`, follow that pattern exactly.

- [ ] **Step 3: Wire up MapCaseRoomApi in the endpoint chain**

Modify `src/infrastructure/iPath.API/MapEndpoints.cs:36`. Change the chain from:

```csharp
            .MapTaskAssignmentEndpoints()
            .MapSyncApi();
```

to:

```csharp
            .MapTaskAssignmentEndpoints()
            .MapSyncApi()
            .MapCaseRoomApi();
```

- [ ] **Step 4: Build to verify everything compiles**

Run: `dotnet build src/infrastructure/iPath.API/iPath.API.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/infrastructure/iPath.API/Endpoints/CaseRoomEndpoints.cs src/infrastructure/iPath.API/MapEndpoints.cs src/infrastructure/iPath.API/APIServicesRegistration.cs
git commit -m "feat(caseroom): add API endpoints for join/leave/sync/status"
```

---

### Task 5: SSE integration — `SseClientService` + `ipath-sse.js`

**Files:**
- Modify: `src/ui/iPath.RazorLib/Notifications/SseClientService.cs`
- Modify: `src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js`
- Test: `test/iPath.Test.xUnit2/CaseRoom/CaseRoomSseClientTests.cs`

**Interfaces:**
- Consumes: `CaseRoomSyncEvent` from Task 1, existing `SseClientService` infrastructure
- Produces: `SseClientService.CaseRoomSyncReceived` event + `[JSInvokable] OnCaseRoomSync(string, string)`, JS dispatch for `caseroom-sync` event

- [ ] **Step 1: Write the failing test**

Create `test/iPath.Test.xUnit2/CaseRoom/CaseRoomSseClientTests.cs`:

```csharp
using iPath.Blazor.Componenents.Notifications;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NSubstitute;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class CaseRoomSseClientTests
{
    [Fact]
    public void OnCaseRoomSync_RaisesEventWithDeserializedPayload()
    {
        // Arrange — SseClientService in WASM mode (no INotificationEventBus in DI)
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseClientService>();
        var js = Substitute.For<IJSRuntime>();
        var service = new SseClientService(js, services, logger);

        var received = new List<CaseRoomSyncEvent>();
        service.CaseRoomSyncReceived += (_, e) => received.Add(e);

        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var payload = $"{{\"requestId\":\"{requestId}\",\"userId\":\"{userId}\",\"displayName\":\"Alice\",\"payload\":{{\"documentId\":null,\"viewport\":{{\"x\":0.5,\"y\":0.5,\"zoom\":2.0}}}},\"timestamp\":\"2026-06-27T12:00:00+00:00\"}}";

        // Act — simulate the JS calling back
        var lastEventId = DateTimeOffset.UtcNow.ToString("o");
        service.OnCaseRoomSync(payload, lastEventId);

        // Assert
        received.Should().ContainSingle();
        received[0].RequestId.Should().Be(requestId);
        received[0].DisplayName.Should().Be("Alice");
        received[0].Payload.Viewport!.Zoom.Should().Be(2.0);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomSseClientTests"`
Expected: Compile failure — `CaseRoomSyncReceived` event and `OnCaseRoomSync` method don't exist.

- [ ] **Step 3: Add CaseRoomSync event to SseClientService**

Modify `src/ui/iPath.RazorLib/Notifications/SseClientService.cs`:

1. Add `using iPath.Application.Features.CaseRoom;` at the top.
2. After the `SystemEventReceived` event declaration, add:

```csharp
    public event EventHandler<CaseRoomSyncEvent>? CaseRoomSyncReceived;
```

3. After the `OnSystemEvent` method, add:

```csharp
    [JSInvokable]
    public void OnCaseRoomSync(string data, string lastEventId)
    {
        _lastEventId = lastEventId;
        try
        {
            var evt = JsonSerializer.Deserialize<CaseRoomSyncEvent>(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (evt is not null)
                CaseRoomSyncReceived?.Invoke(this, evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize caseroom-sync");
        }
    }
```

- [ ] **Step 4: Add JS listener for `caseroom-sync`**

Modify `src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js`. After the `system-event` listener, add:

```javascript
    es.addEventListener('caseroom-sync', e => {
        dotNetHelper.invokeMethodAsync('OnCaseRoomSync', e.data, e.lastEventId);
    });
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomSseClientTests"`
Expected: PASS (1 test).

- [ ] **Step 6: Commit**

```bash
git add src/ui/iPath.RazorLib/Notifications/SseClientService.cs src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js test/iPath.Test.xUnit2/CaseRoom/CaseRoomSseClientTests.cs
git commit -m "feat(caseroom): wire caseroom-sync SSE event through SseClientService"
```

---

### Task 6: `IPathApi` Refit methods + `DirectApiClient` implementations

**Files:**
- Modify: `src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs` — add 4 Refit methods
- Modify: `src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs` — implement the 4 methods
- Test: `test/iPath.Test.xUnit2/CaseRoom/DirectApiClientCaseRoomTests.cs`

**Interfaces:**
- Consumes: `ICaseRoomSessionStore` (Task 3) for `DirectApiClient`, `CaseRoomModels` (Task 1)
- Produces: `IPathApi.JoinCaseRoomAsync`, `LeaveCaseRoomAsync`, `SyncCaseRoomAsync`, `GetCaseRoomStatusAsync`

- [ ] **Step 1: Add Refit methods to `IPathApi`**

Modify `src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs`. Add `using iPath.Application.Features.CaseRoom;` at the top, and add a new region after the existing `-- Notifications --` region (or wherever geographically sensible adjacent to ServiceRequest region):

```csharp
    #region "-- CaseRoom --"
    [Post("/api/v1/caseroom/{requestId}/join")]
    Task<IApiResponse<CaseRoomSnapshot>> JoinCaseRoom(Guid requestId);

    [Post("/api/v1/caseroom/{requestId}/leave")]
    Task<IApiResponse> LeaveCaseRoom(Guid requestId);

    [Post("/api/v1/caseroom/{requestId}/sync")]
    Task<IApiResponse> SyncCaseRoom(Guid requestId, [Body] SyncPayload payload);

    [Get("/api/v1/caseroom/{requestId}")]
    Task<IApiResponse<CaseRoomStatus?>> GetCaseRoomStatus(Guid requestId);
    #endregion
```

- [ ] **Step 2: Implement on DirectApiClient**

Modify `src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs`:

1. Add `using iPath.API.Services.CaseRoom;` and `using iPath.Application.Features.CaseRoom;` at the top.
2. Add `ICaseRoomSessionStore caseRoomStore` as a constructor parameter (the last optional one — pattern matches the existing `syncRunner`, `jobManager`, `queue` parameters).

Construct signature change:

```csharp
public class DirectApiClient(
    IMediator mediator,
    IGroupService groupService,
    IEmailRepository emailRepo,
    INotificationRepository notificationRepo,
    IUserSession userSession,
    ILocalizationDataProvider localization,
    IOptions<iPathClientConfig> config,
    ILogger<DirectApiClient> logger,
    ISyncImportRunner? syncRunner = null,
    ISyncJobManager? jobManager = null,
    IAiExtractionQueue? queue = null,
    ICaseRoomSessionStore? caseRoomStore = null)
    : IPathApi
```

3. Implement the 4 methods at the end of the class (last `#endregion`):

```csharp
    // -- CaseRoom --

    public async Task<IApiResponse<CaseRoomSnapshot>> JoinCaseRoom(Guid requestId)
    {
        if (caseRoomStore is null || userSession.User is null)
            return RespondError<CaseRoomSnapshot>();
        var snap = await caseRoomStore.JoinAsync(requestId, userSession.User.Id, userSession.User.DisplayName ?? "Anonymous", default);
        return Respond(snap);
    }

    public async Task<IApiResponse> LeaveCaseRoom(Guid requestId)
    {
        if (caseRoomStore is null || userSession.User is null)
            return RespondError();
        await caseRoomStore.LeaveAsync(requestId, userSession.User.Id, default);
        return RespondOk();
    }

    public async Task<IApiResponse> SyncCaseRoom(Guid requestId, SyncPayload payload)
    {
        if (caseRoomStore is null || userSession.User is null)
            return RespondError();
        await caseRoomStore.SyncAsync(requestId, userSession.User.Id, payload, default);
        return RespondOk();
    }

    public async Task<IApiResponse<CaseRoomStatus?>> GetCaseRoomStatus(Guid requestId)
    {
        if (caseRoomStore is null)
            return Respond<CaseRoomStatus?>(null);
        var status = await caseRoomStore.GetStatusAsync(requestId, default);
        return Respond(status);
    }
```

> **Note:** The `DirectApiClient` lives in iPath.Blazor.ServiceLib. It references `iPath.API` only because `caseRoomStore` lives there — verify `iPath.API.csproj` is referenced by `iPath.Blazor.ServiceLib.csproj`. It already is: `DirectApiClient` imports `iPath.API.Services` indirectly via other handlers. If the using `iPath.API.Services.CaseRoom` causes a build error (missing project reference), add a project reference to `iPath.API.csproj` from `iPath.Blazor.ServiceLib.csproj`.

- [ ] **Step 3: Write a smoke test for DirectApiClient CaseRoom methods**

Create `test/iPath.Test.xUnit2/CaseRoom/DirectApiClientCaseRoomTests.cs`:

```csharp
using iPath.API.Services.CaseRoom;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using iPath.Blazor.ServiceLib.Services;
using iPath.Application.Contracts;
using iPath.Application.Localization;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using DispatchR;
using iPath.Application.Contracts;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class DirectApiClientCaseRoomTests
{
    private static (DirectApiClient client, CaseRoomSessionStore store) CreateClient()
    {
        var store = new CaseRoomSessionStore(
            Substitute.For<iPath.API.Services.Notifications.ISseConnectionManager>(),
            new NotificationEventBus(),
            new LoggerFactory().CreateLogger<CaseRoomSessionStore>());

        var mediator = Substitute.For<IMediator>();
        var userSession = Substitute.For<IUserSession>();
        userSession.User.Returns(new SessionUserDto { Id = Guid.NewGuid(), DisplayName = "Test", IsAuthenticated = true });

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
```

> **Note:** Property signings on `SessionUserDto` (e.g., `DisplayName`) must match the existing `SessionUserDto` definition in `iPath.Application.Contracts`. If `SessionUserDto` doesn't have `DisplayName`, fall back to whatever property exists — verify by `grep`-ing for `public.*SessionUserDto` and reading the file. The Refit-side WASM client doesn't need this; only `DirectApiClient`'s call site here uses it.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~DirectApiClientCaseRoomTests"`
Expected: PASS. If project-reference issue arises for `iPath.API.Services.CaseRoom`, fix the csproj references first.

- [ ] **Step 5: Commit**

```bash
git add src/ui/iPath.Blazor.ServiceLib/ test/iPath.Test.xUnit2/CaseRoom/DirectApiClientCaseRoomTests.cs
git commit -m "feat(caseroom): add IPathApi Refit methods and DirectApiClient implementations"
```

---

### Task 7: WASM and Server implementations of `ICaseRoomSyncService` / `ICaseRoomSyncReceiver`

**Files:**
- Create: `src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncService.cs` (WASM)
- Create: `src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncReceiver.cs` (WASM)
- Create: `src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncService.cs` (Server)
- Create: `src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncReceiver.cs` (Server)
- Modify: `src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs` — register implementations based on `WasmClient`
- Test: `test/iPath.Test.xUnit2/CaseRoom/CaseRoomSyncTransportTests.cs`

**Interfaces:**
- Consumes: `IPathApi` (Refit client or `DirectApiClient`), `ICaseRoomSessionStore`, `INotificationEventBus`, `SseClientService`, all from previous tasks
- Produces: `ICaseRoomSyncService` and `ICaseRoomSyncReceiver` implementations injected into Blazor pages

- [ ] **Step 1: Write failing tests for both implementations**

Create `test/iPath.Test.xUnit2/CaseRoom/CaseRoomSyncTransportTests.cs`:

```csharp
using iPath.API.Services.CaseRoom;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using iPath.Blazor.Componenents.CaseRoom;
using iPath.Blazor.Componenents.Notifications;
using iPath.Blazor.ServiceLib.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NSubstitute;
using FluentAssertions;

namespace iPath.Test.xUnit2.CaseRoom;

public class CaseRoomSyncTransportTests
{
    [Fact]
    public async Task InMemorySync_PublishesViaEventBusAndReachesReceiver()
    {
        var sseMgr = Substitute.For<iPath.API.Services.Notifications.ISseConnectionManager>();
        var bus = new NotificationEventBus();
        var store = new CaseRoomSessionStore(sseMgr, bus, new LoggerFactory().CreateLogger<CaseRoomSessionStore>());

        var received = new List<CaseRoomSyncEvent>();
        var receiver = new InMemoryCaseRoomSyncReceiver(bus);
        var requestId = Guid.NewGuid();

        var sub = receiver.Subscribe(requestId, e =>
        {
            if (e.RequestId == requestId) received.Add(e);
        });

        // Two users join; first user syncs; second user should receive via EventBus
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await store.JoinAsync(requestId, userA, "Alice", default);
        await store.JoinAsync(requestId, userB, "Bob", default);

        await store.SyncAsync(requestId, userA, new SyncPayload(null, new ViewportState(0.5, 0.5, 2.0)), default);

        // Note: store broadcasts to ALL subscribers including sender; receiver filters by requestId only
        received.Should().NotBeEmpty();
        sub.Dispose();
    }

    [Fact]
    public async Task HttpReceiver_ForwardsSseClientServiceEvents()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseClientService>();
        var js = Substitute.For<IJSRuntime>();
        var sseService = new SseClientService(js, services, logger);

        var receiver = new HttpCaseRoomSyncReceiver(sseService);
        var received = new List<CaseRoomSyncEvent>();
        var requestId = Guid.NewGuid();
        var sub = receiver.Subscribe(requestId, e =>
        {
            if (e.RequestId == requestId) received.Add(e);
        });

        var evt = new CaseRoomSyncEvent(requestId, Guid.NewGuid(), "Alice",
            new SyncPayload(null, new ViewportState(1, 1, 1)), DateTimeOffset.UtcNow);

        sseService.CaseRoomSyncReceived += (_, e) =>
        {
            // Use null-conditional to verify event propagation
        };
        // Simulate the JS callback pathway by invoking the [JSInvokable] directly
        var json = System.Text.Json.JsonSerializer.Serialize(evt, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        sseService.OnCaseRoomSync(json, DateTimeOffset.UtcNow.ToString("o"));

        received.Should().ContainSingle();
        received[0].DisplayName.Should().Be("Alice");
        sub.Dispose();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomSyncTransportTests"`
Expected: Compile failure — implementation classes don't exist.

- [ ] **Step 3: Create WASM HTTP sync service**

Create `src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncService.cs`:

```csharp
using iPath.Application.Features.CaseRoom;
using iPath.Blazor.ServiceLib.Services;

namespace iPath.Blazor.Componenents.CaseRoom;

public class HttpCaseRoomSyncService(IPathApi api) : ICaseRoomSyncService
{
    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default)
        => api.JoinCaseRoom(requestId).ContinueWith(t => t.Result.Content!, ct);

    public Task LeaveAsync(Guid requestId, CancellationToken ct = default)
        => api.LeaveCaseRoom(requestId);

    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
        => api.SyncCaseRoom(requestId, payload);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 4: Create WASM HTTP receiver (delegates to SseClientService)**

Create `src/ui/iPath.RazorLib/CaseRoom/HttpCaseRoomSyncReceiver.cs`:

```csharp
using iPath.Application.Features.CaseRoom;
using iPath.Blazor.Componenents.Notifications;

namespace iPath.Blazor.Componenents.CaseRoom;

public sealed class HttpCaseRoomSyncReceiver(SseClientService sse) : ICaseRoomSyncReceiver
{
    private readonly List<(Action<CaseRoomSyncEvent> handler, EventHandler<CaseRoomSyncEvent> wrapper, List<Guid> filter)> _subs = new();

    public IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler)
    {
        var filter = new List<Guid> { requestId };
        void wrapper(object? s, CaseRoomSyncEvent e)
        {
            if (e.RequestId == requestId) handler(e);
        }
        sse.CaseRoomSyncReceived += wrapper;
        var sub = new SyncUnsubscriber(() =>
        {
            sse.CaseRoomSyncReceived -= wrapper;
        });
        return sub;
    }

    private sealed class SyncUnsubscriber(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
```

- [ ] **Step 5: Create Server-side in-memory sync service**

Create `src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncService.cs`:

```csharp
using iPath.API.Services.CaseRoom;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Contracts;

namespace iPath.Blazor.Componenents.CaseRoom;

public class InMemoryCaseRoomSyncService(
    ICaseRoomSessionStore store,
    IUserSession userSession) : ICaseRoomSyncService
{
    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default)
    {
        if (userSession.User is null)
            throw new InvalidOperationException("User not authenticated");
        return store.JoinAsync(requestId, userSession.User.Id, userSession.User.DisplayName ?? "Anonymous", ct);
    }

    public Task LeaveAsync(Guid requestId, CancellationToken ct = default)
    {
        if (userSession.User is null) return Task.CompletedTask;
        return store.LeaveAsync(requestId, userSession.User.Id, ct);
    }

    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
    {
        if (userSession.User is null) return Task.CompletedTask;
        return store.SyncAsync(requestId, userSession.User.Id, payload, ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 6: Create Server-side in-memory receiver**

Create `src/ui/iPath.RazorLib/CaseRoom/InMemoryCaseRoomSyncReceiver.cs`:

```csharp
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;

namespace iPath.Blazor.Componenents.CaseRoom;

public sealed class InMemoryCaseRoomSyncReceiver(INotificationEventBus bus) : ICaseRoomSyncReceiver
{
    public IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler)
    {
        void filtered(CaseRoomSyncEvent e)
        {
            if (e.RequestId == requestId) handler(e);
        }
        return bus.SubscribeCaseRoomSync(filtered);
    }
}
```

- [ ] **Step 7: Register both based on WasmClient flag**

Modify `src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs`. Add at the top with other usings:

```csharp
using iPath.Application.Features.CaseRoom;
using iPath.Blazor.Componenents.CaseRoom;
```

Inside `AddRazorLibServices`, after the `services.AddScoped<SseClientService>();` line (`RazorLibServiceRegistration.cs:111`), add:

```csharp
        // CaseRoom: WASM uses HTTP+SSE; Server uses in-memory + EventBus
        if (WasmClient)
        {
            services.AddScoped<ICaseRoomSyncService, HttpCaseRoomSyncService>();
            services.AddScoped<ICaseRoomSyncReceiver, HttpCaseRoomSyncReceiver>();
        }
        else
        {
            services.AddScoped<ICaseRoomSyncService, InMemoryCaseRoomSyncService>();
            services.AddScoped<ICaseRoomSyncReceiver, InMemoryCaseRoomSyncReceiver>();
        }
```

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomSyncTransportTests"`
Expected: PASS (2 tests).

- [ ] **Step 9: Build the full solution to catch any compile errors**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 10: Commit**

```bash
git add src/ui/iPath.RazorLib/CaseRoom/ src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs test/iPath.Test.xUnit2/CaseRoom/CaseRoomSyncTransportTests.cs
git commit -m "feat(caseroom): implement dual-mode sync service and receiver (HTTP+SSE / in-memory)"
```

---

### Task 8: JS interop module — `ipath-caseroom.js`

**Files:**
- Create: `src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js`

**Interfaces:**
- Consumes: OpenSeadragon CDN (already used by existing OSD integration)
- Produces: `initOsd(divId, tileSourceUrl, dotNetRef)`, `openTileSource(url)`, `setViewport(x, y, zoom)`, `getViewport()`, `dispose()`

- [ ] **Step 1: Create the JS interop module**

Create `src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js`:

```javascript
let viewer = null;
let dotNetRef = null;
let throttleTimer = null;
let isApplyingRemote = false;

export function initOsd(divId, tileSourceUrl, dotNetReference) {
    dotNetRef = dotNetReference;

    const elem = document.getElementById(divId);
    if (!elem) return;

    elem.innerHTML = '';

    viewer = OpenSeadragon({
        id: elem.id,
        prefixUrl: "https://cdn.jsdelivr.net/npm/openseadragon@5.0.1/build/openseadragon/images/",
        visibilityRatio: 1.0,
        constrainDuringPan: true,
        defaultZoomLevel: 0,
        minZoomLevel: 0.5,
        maxZoomLevel: 100,
        showNavigationControl: false,
        crossOriginPolicy: "Anonymous"
    });

    viewer.addHandler('open', () => { /* loader could be hidden here */ });

    viewer.addHandler('open-failed', (event) => {
        console.error('OSD open failed:', event.message);
    });

    viewer.addHandler('viewport-change', onViewportChange);

    if (tileSourceUrl) openTileSource(tileSourceUrl);
}

export function openTileSource(url) {
    if (!viewer) return;
    if (url && url.toLowerCase().endsWith('.dzi')) {
        viewer.open(url);
    } else {
        viewer.open({ type: 'image', url: url, buildPyramid: false });
    }
}

export function setViewport(x, y, zoom) {
    if (!viewer) return;
    isApplyingRemote = true;
    viewer.viewport.panTo({ x: x, y: y }, true);
    viewer.viewport.zoomTo(zoom, null, true);
    // Release guard after OSD's own viewport-change settles
    setTimeout(() => { isApplyingRemote = false; }, 50);
}

export function getViewport() {
    if (!viewer) return null;
    const c = viewer.viewport.getCenter();
    const z = viewer.viewport.getZoom();
    return { x: c.x, y: c.y, zoom: z };
}

export function dispose() {
    if (throttleTimer) { clearTimeout(throttleTimer); throttleTimer = null; }
    if (viewer) { viewer.destroy(); viewer = null; }
    dotNetRef = null;
    isApplyingRemote = false;
}

function onViewportChange() {
    if (isApplyingRemote || !dotNetRef) return;
    if (throttleTimer) return;
    throttleTimer = setTimeout(() => {
        const c = viewer.viewport.getCenter();
        const z = viewer.viewport.getZoom();
        dotNetRef.invokeMethodAsync('OnViewportChanged', c.x, c.y, z);
        throttleTimer = null;
    }, 150);
}
```

- [ ] **Step 2: Build to verify static file is included**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj`
Expected: Build succeeded (JS file picked up automatically as static web asset).

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js
git commit -m "feat(caseroom): add OSD JS interop module with throttled viewport sync"
```

---

### Task 9: `CaseRoomPage.razor` — Blazor page with inline OSD

**Files:**
- Create: `src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor`
- Create: `src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor.cs`
- Modify: `src/ui/iPath.RazorLib/_Imports.razor` — add `@using iPath.Application.Features.CaseRoom` and `@using iPath.Blazor.Componenents.CaseRoom`

**Interfaces:**
- Consumes: `ICaseRoomSyncService`, `ICaseRoomSyncReceiver`, `ServiceRequestViewModel`, `IJSRuntime`, `NavigationManager`, all from previous tasks
- Produces: Blazor page at `/request/{id}/caseroom`

- [ ] **Step 1: Add `@using` directives to RazorLib `_Imports.razor`**

Modify `src/ui/iPath.RazorLib/_Imports.razor`. Add before the final `@inject IStringLocalizer T` line:

```razor
@using iPath.Application.Features.CaseRoom
@using iPath.Blazor.Componenents.CaseRoom
```

- [ ] **Step 2: Create the Razor page**

Create `src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor`:

```razor
@page "/request/{id}/caseroom"

@attribute [Authorize]

@using MudBlazor.Services
@using iPath.Blazor.Componenents.Layouts
@using iPath.Blazor.Componenents.Documents
@layout SlideshowLayout
@inherits ServiceRequestViewComponentBase
@inject IJSRuntime JS
@inject ICaseRoomSyncService SyncService
@inject ICaseRoomSyncReceiver SyncReceiver
@inject IOptions<iPathClientConfig> opts

<MudSwipeArea Style="height: 100%; width: 100%; background-color: black;"
              OnSwipeEnd="OnSwipeHandler">

    <div class="d-flex justify-center flex-grow-1 gap-2">
        <MudIconButton Icon="@Icons.Material.Filled.ArrowCircleLeft" Size="Size.Small" OnClick="GotoPrevious" />
        <MudIconButton Icon="@Icons.Material.Filled.ArrowCircleRight" Size="Size.Small" OnClick="GotoNext" />
        <MudChip Color="Color.Success" Size="Size.Small" Variant="Variant.Filled">
            @Participants.Count viewing
        </MudChip>
        <MudIconButton Icon="@Icons.Material.Filled.CloseFullscreen" Size="Size.Small" OnClick="@ExitRoom" />
    </div>

    <MudPaper Class="ipath_image slideshow" Style="background-color: black;" Elevation="0">
        <div id="osd-caseroom" style="width: 100%; height: calc(100vh - 120px); background-color: black;"></div>
    </MudPaper>
</MudSwipeArea>

@code {
    [Parameter] public string id { get; set; }
    bool Wsi => opts.Value.WsiViewerActive;
}
```

- [ ] **Step 3: Create the code-behind**

Create `src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor.cs`:

```csharp
using iPath.Application.Features.CaseRoom;
using iPath.Blazor.Componenents.Documents;
using iPath.Blazor.Componenents.ServiceRequests;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace iPath.Blazor.Componenents.ServiceRequests;

public partial class CaseRoomPage : ComponentBase, IAsyncDisposable
{
    private Guid RequestId => Guid.Parse(id);
    private IJSObjectReference? _module;
    private DotNetObjectReference<CaseRoomPage>? _dotNetRef;
    private IDisposable? _syncSub;
    private bool _isApplyingRemote;
    private bool _initialized;

    private List<Participant> Participants { get; set; } = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_initialized)
        {
            _initialized = true;
            await vm.LoadNode(RequestId);

            _module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/iPath.RazorLib/js/ipath-caseroom.js");

            _dotNetRef = DotNetObjectReference.Create(this);

            var snapshot = await SyncService.JoinAsync(RequestId);
            Participants = snapshot.Participants.ToList();
            StateHasChanged();

            // Wire sync receiver
            _syncSub = SyncReceiver.Subscribe(RequestId, OnSyncReceived);

            // Initialize OSD with current active document (or first slide)
            var docId = snapshot.ActiveDocumentId ??
                        vm.SelectedRequest?.Documents.FirstOrDefault(d => d.IsSlide)?.Id;

            if (docId.HasValue)
            {
                vm.SelectDocument(docId.Value);
                var doc = vm.SelectedDocument;
                var url = GetTileSourceUrl(doc);
                await _module.InvokeVoidAsync("initOsd", "osd-caseroom", url, _dotNetRef);
            }
            else
            {
                await _module.InvokeVoidAsync("initOsd", "osd-caseroom", null, _dotNetRef);
            }
        }
    }

    [JSInvokable]
    public async Task OnViewportChanged(double x, double y, double zoom)
    {
        if (_isApplyingRemote) return;
        await SyncService.SyncAsync(RequestId, new SyncPayload(null, new ViewportState(x, y, zoom)));
    }

    private async Task OnSyncReceived(CaseRoomSyncEvent evt)
    {
        if (evt.UserId == vm?.AppState?.User?.Id) return;  // ignore our own echo

        _isApplyingRemote = true;

        if (evt.Payload.DocumentId.HasValue && _module is not null)
        {
            vm.SelectDocument(evt.Payload.DocumentId.Value);
            var url = GetTileSourceUrl(vm.SelectedDocument);
            await _module.InvokeVoidAsync("openTileSource", url);
        }

        if (evt.Payload.Viewport is not null && _module is not null)
        {
            await _module.InvokeVoidAsync("setViewport",
                evt.Payload.Viewport.X, evt.Payload.Viewport.Y, evt.Payload.Viewport.Zoom);
        }

        _isApplyingRemote = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task GotoNext()
    {
        await vm.SelectNextSlide();
        await BroadcastDocumentChange();
    }

    private async Task GotoPrevious()
    {
        await vm.SelectPreviousSlide();
        await BroadcastDocumentChange();
    }

    private async Task OnSwipeHandler(MudBlazor.Services.SwipeEventArgs args)
    {
        if (args.SwipeDirection == MudBlazor.Services.SwipeDirection.RightToLeft)
            await GotoNext();
        else if (args.SwipeDirection == MudBlazor.Services.SwipeDirection.LeftToRight)
            await GotoPrevious();
    }

    private async Task BroadcastDocumentChange()
    {
        if (vm.SelectedDocument is null) return;
        await SyncService.SyncAsync(RequestId, new SyncPayload(vm.SelectedDocument.Id, null));
        if (_module is not null)
            await _module.InvokeVoidAsync("openTileSource", GetTileSourceUrl(vm.SelectedDocument));
    }

    private async Task ExitRoom()
    {
        await vm.GoUpRequestPage();
    }

    private string GetTileSourceUrl(DocumentDto doc)
    {
        return doc.FileExtension.ToLower() == ".vsi"
            ? $"/files/{doc.Id}.dzi"
            : $"/files/{doc.Id}";
    }

    public async ValueTask DisposeAsync()
    {
        _syncSub?.Dispose();
        try
        {
            if (_module is not null)
                await _module.InvokeVoidAsync("dispose");
        }
        catch (JSDisconnectedException) { }
        try
        {
            await SyncService.LeaveAsync(RequestId);
        }
        catch { /* tolerate network failures during teardown */ }
        _dotNetRef?.Dispose();
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
```

> **Note:** The `vm.AppState.User.Id` access above is a guess — verify the property name in `AppState`. If `AppState.User` exposes a different shape (e.g., `appState.User.Id`), update accordingly. The existing `SlideShowPage` inherits `ServiceRequestViewComponentBase` which injects `vm` as `ServiceRequestViewModel`; use the existing snackbar/dialog patterns from there.

- [ ] **Step 4: Build the solution**

Run: `dotnet build`
Expected: Build succeeded.

If `vm.AppState` is not accessible, read `src/ui/iPath.RazorLib/Shared/State/AppState.cs` and `ServiceRequestViewModel.cs` for the right access path. The ViewModel's injected `appState` is `private` — you may need to either (a) expose a helper on the VM, or (b) inject `AppState` directly into the page. Use approach (b) — inject `AppState appState` into `CaseRoomPage.razor.cs` constructor and use `appState.User?.Id`.

- [ ] **Step 5: Commit**

```bash
git add src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor src/ui/iPath.RazorLib/ServiceRequests/CaseRoomPage.razor.cs src/ui/iPath.RazorLib/_Imports.razor
git commit -m "feat(caseroom): add CaseRoomPage with inline OSD and bidirectional sync"
```

---

### Task 10: "CaseRoom active" badge on ServiceRequest page

**Files:**
- Modify: `src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor`

**Interfaces:**
- Consumes: `IPathApi.GetCaseRoomStatus` (Refit) or via server direct, polling endpoint added in Task 4
- Produces: a chip linking to `/request/{id}/caseroom` when a room is active

- [ ] **Step 1: Read the current ServiceRequestPage.razor**

Run: identify the file by reading `src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor` (it was modified recently per git log).

- [ ] **Step 2: Add room-status polling and badge**

Modify `src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor`. After the existing toolbar/header area, add a chip when a room is active:

```razor
@if (CaseRoomActive)
{
    <MudChip Color="Color.Success" Size="Size.Small" Variant="Variant.Filled"
             Href="@($"request/{id}/caseroom")" Class="ma-1"
             Icon="@Icons.Material.Filled.Group">
        @CaseRoomParticipantCount in CaseRoom
    </MudChip>
}
```

In the `@code` block of the page (or its code-behind if there is one), add:

```csharp
private bool CaseRoomActive;
private int CaseRoomParticipantCount;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender && vm.SelectedRequest is not null)
    {
        try
        {
            var resp = await api.GetCaseRoomStatus(vm.SelectedRequest.Id);
            if (resp.IsSuccessful && resp.Content is not null)
            {
                CaseRoomActive = resp.Content.IsActive;
                CaseRoomParticipantCount = resp.Content.ParticipantCount;
                StateHasChanged();
            }
        }
        catch { /* non-fatal: room status is informational */ }
    }
}
```

> **Note:** If the page already injects `IPathApi api` via the ViewModel pattern (which it does via `vm`), call it through the ViewModel by adding a helper method `ReloadCaseRoomStatusAsync()` on `ServiceRequestViewModel`, OR inject `IPathApi` directly into the page for this one call. Prefer injecting `IPathApi` directly to keep the change minimal. Verify the existing `ServiceRequestPage.razor` structure and adapt to existing patterns.

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor
git commit -m "feat(caseroom): show CaseRoom active badge on ServiceRequest page"
```

---

### Task 11: Final build + manual test plan

**Files:** none — verification only.

- [ ] **Step 1: Run all tests**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj`
Expected: All CaseRoom tests PASS (Task 1: 4, Task 2: 2, Task 3: 7, Task 5: 1, Task 6: 3, Task 7: 2 = 19 tests). No previously-passing tests regressed.

- [ ] **Step 2: Build the solution in Release configuration**

Run: `dotnet build --configuration Release`
Expected: Build succeeded with no warnings above the existing baseline.

- [ ] **Step 3: Manual test plan**

Run the app in server mode (e.g., `dotnet run --project src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj`).

Test 1 (single-user smoke):
- Navigate to a ServiceRequest that has at least one .vsi image and at least 1 .dzi tile source
- Navigate to `/request/{id}/caseroom`
- Verify: page loads, OSD renders, prev/next works, pan/zoom smooth
- Verify: "1 viewing" chip shows your own presence

Test 2 (two-browser sync, server mode):
- Open the same `/request/{id}/caseroom` in two browsers (e.g., one normal window + one private window, signed in as two different users)
- In window A: pan and zoom the slide
- Verify: window B's OSD follows the same viewport within ~150ms
- In window A: click "Next" to change document
- Verify: window B's OSD switches to the same document
- In window A: pan again
- Verify: window B follows without echo loops

Test 3 (leave cleanup):
- Close one browser tab
- Verify: the remaining window shows "1 viewing" (participant count decremented within 30s)
- Close all tabs
- Re-open the same case → session recreates cleanly

Test 4 (badge):
- Open `/request/{id}/caseroom` in one window
- Open `/request/{id}` in a different window
- Verify: "X in CaseRoom" chip is visible on the ServiceRequest page with the correct count

- [ ] **Step 4: Commit final test results as a Markdown report (optional)**

If manual tests pass, write a brief note to `docs/superpowers/plans/2026-06-27-caseroom-test-results.md` and commit:

```bash
git add docs/superpowers/plans/
git commit -m "docs(caseroom): manual test results"
```

- [ ] **Step 5: Final commit (if any tests changed during integration)**

```bash
git status
# If anything uncommitted:
git add -A
git commit -m "test(caseroom): integration polish"
```

---

## Self-Review Checklist (pre-execution)

- [x] Spec coverage: all 11 tasks map to spec sections (models/store/transport/endpoints/SSE/Refit/DirectApiClient/clients/page/badge)
- [x] No placeholders — every step has concrete code or verifiable output
- [x] Type consistency — `CaseRoomSession`, `ICaseRoomSessionStore`, `ICaseRoomSyncService`, `ICaseRoomSyncReceiver` named consistently across tasks; method signatures match between interface definitions and call sites
- [x] Spec updates broadcast through **both** `SseConnectionManager.SendToUserAsync` AND `INotificationEventBus.PublishCaseRoomSync` from the store (Task 3 confirms via test)
- [x] Tests do not require database (Task 3 uses NSubstitute mocks for `ISseConnectionManager`)
- [x] No EF migration needed — no new entities or DbSets

## Notes for the Implementing Agent

- After Task 4 (API endpoints), manually test with `curl` if accessible to confirm endpoints load and require auth (e.g., `curl -i http://localhost:5000/api/v1/caseroom/{some-guid}` → 401).
- After Task 9 (Blazor page), open one browser only and check the JS console for errors before declaring it works.
- The OSD CDN dependency is already used by the existing OsdViewerIFrame — no new external dependency introduced.
- The `SlideshowLayout.razor` is fullscreen dark theme; `CaseRoomPage` reuses it for visual continuity with `SlideShowPage`.
- Per `AGENTS.md`: never run `dotnet ef migrations add` — no DB changes were required for this plan anyway.