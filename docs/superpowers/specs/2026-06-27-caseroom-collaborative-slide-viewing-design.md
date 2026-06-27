# CaseRoom — Collaborative Real-Time Slide Viewing

**Date:** 2026-06-27
**Status:** Approved
**Branch:** `feature/caseroom-collaborative-slide-viewing`

## Problem

Pathologists need to review whole-slide images (WSI) together in real time. Today, iPath.NET's `SlideShowPage` is a single-user experience. The `viewer-compare.html` demo proves the concept of syncing two OSD viewers within one browser, but there is no mechanism to synchronize viewport state (pan, zoom, document selection) across multiple users over the network.

## Solution

A Blazor-integrated "CaseRoom" feature: one shared, real-time slide-viewing session per `ServiceRequest`. Any authorized group member can join; everyone sees the same slide at the same pan/zoom position. Shared control (any participant can navigate). The session is in-memory, transient, and piggybacks on existing SSE infrastructure.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| UI host | Blazor page (Approach A) | Mirrors existing patterns; full access to auth, groups, config |
| Control model | Shared board (any participant drives) | Simplest; rules can be added later |
| Sync scope | Document selection + pan/zoom | Core "shared board" experience; annotations/chat/markers deferred |
| Session identity | One room per ServiceRequestId | Sufficient for v1; separate session IDs can be layered later |
| Persistence | In-memory only | Transient sessions; no DB schema changes. Future `LiveSession` entity can be added |
| Transport | Abstraction with pluggable implementations | WASM → REST+SSE; Server → in-process; later → SignalR |
| OSD integration | Inline JS interop (no iframe) | Clean bidirectional bridge between Blazor and OSD; replaces the iframe-based `OsdViewerIFrame` proof-of-concept |

## Architecture

### Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Blazor Page                              │
│  /request/{id}/caseroom                                      │
│                                                              │
│  ┌──────────────┐   ┌──────────────────────────────────────┐ │
│  │  UI Controls │   │     OSD Canvas (<div id="osd">)       │ │
│  │  (prev/next,  │   │  Initialized via ipath-caseroom.js   │ │
│  │  participants)│   │  ┌─ viewport-change ──→ throttle ──┐  │ │
│  │               │   │  │    panTo/zoomTo ←── JS interop  │  │ │
│  └──────────────┘   └──────────────────────────────────────┘ │
│         │                        │                            │
│         ▼                        ▼                            │
│  ┌──────────────────────────────────────────┐                 │
│  │  CaseRoomViewModel                       │                 │
│  │  (Blazor component state)                │                 │
│  │  - ActiveDocument, Participants          │                 │
│  │  - SendSync() / OnSyncReceived()         │                 │
│  └──────────────────────────────────────────┘                 │
│         │                        ▲                            │
│         ▼                        │                            │
│  ┌──────────────────────────────────────────┐                 │
│  │  ICaseRoomSyncService (send)              │                 │
│  │  ICaseRoomSyncReceiver (receive)          │                 │
│  │  ┌─────────────┐  ┌────────────────────┐  │                 │
│  │  │ WASM impl   │  │ Server impl        │  │                 │
│  │  │ REST + SSE  │  │ In-memory + EventBus│ │                 │
│  │  └─────────────┘  └────────────────────┘  │                 │
│  └──────────────────────────────────────────┘                 │
│         │                                                    │
└─────────┼────────────────────────────────────────────────────┘
          │
          ▼  (WASM: HTTP)
          ▼  (Server: in-process)
┌─────────────────────────────────────────────────────────────┐
│                     API Layer (iPath.API)                    │
│                                                              │
│  CaseRoomEndpoints                                           │
│  - POST /api/v1/caseroom/{requestId}/join                    │
│  - POST /api/v1/caseroom/{requestId}/leave                   │
│  - POST /api/v1/caseroom/{requestId}/sync                    │
│  - GET  /api/v1/caseroom/{requestId}                         │
│                                                              │
│  ICaseRoomSessionStore (singleton, in-memory)                │
│  - ConcurrentDictionary<requestId, CaseRoomSession>         │
│  - Join / Leave / UpdateViewport / ChangeDocument / Snapshot │
│  - Broadcasts via SseConnectionManager (WASM) or             │
│    INotificationEventBus (Server)                            │
└─────────────────────────────────────────────────────────────┘
```

### Components

#### 1. Session Store — `ICaseRoomSessionStore`

Singleton, in-memory, mirrors `SseConnectionManager` pattern.

```csharp
public interface ICaseRoomSessionStore
{
    Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid userId, string displayName, CancellationToken ct);
    Task LeaveAsync(Guid requestId, Guid userId, CancellationToken ct);
    Task SyncAsync(Guid requestId, Guid userId, SyncPayload payload, CancellationToken ct);
    Task<CaseRoomStatus?> GetStatusAsync(Guid requestId, CancellationToken ct);
}

public record CaseRoomSession
{
    public Guid RequestId { get; init; }
    public Guid? ActiveDocumentId { get; set; }
    public ViewportState? CurrentViewport { get; set; }
    public Dictionary<Guid, Participant> Participants { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
    public Guid CreatedBy { get; init; }
    public CancellationTokenSource? TeardownCts { get; set; }
}

public record ViewportState(double X, double Y, double Zoom);

public record Participant(Guid UserId, string DisplayName, DateTimeOffset JoinedAt);

public record SyncPayload(Guid? DocumentId, ViewportState? Viewport);

public record CaseRoomSnapshot(Guid RequestId, Guid? ActiveDocumentId, ViewportState? Viewport, Participant[] Participants);

public record CaseRoomStatus(bool IsActive, int ParticipantCount, string[] ParticipantNames);
```

**Lifecycle:**
- `JoinAsync`: creates session if none exists; adds participant; cancels any pending teardown timer.
- `LeaveAsync`: removes participant; if participants == 0, starts 30s teardown timer (tolerates page refresh).
- `SyncAsync`: updates `ActiveDocumentId` or `CurrentViewport` in the session; broadcasts `CaseRoomSyncEvent` to all other participants.
- `GetStatusAsync`: lightweight snapshot for "room active" badge.

**Broadcast routing:**
- The store needs to reach both WASM clients (via `SseConnectionManager.SendToUserAsync`) and Server-mode clients (via `INotificationEventBus.PublishCaseRoomSync`). Both are injected as dependencies; the store always broadcasts through **both** channels. In a mixed Auto-render deployment, some users may be on WASM (reached via SSE) and others on Server (reached via EventBus). Each channel only reaches users connected through it; the other is a no-op if no one is listening.

#### 2. Transport Abstraction — `ICaseRoomSyncService` + `ICaseRoomSyncReceiver`

```csharp
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

public record CaseRoomSyncEvent(Guid RequestId, Guid UserId, string DisplayName, SyncPayload Payload, DateTimeOffset Timestamp);
```

**WASM implementation** (`HttpCaseRoomSyncService`):
- `JoinAsync` / `LeaveAsync` / `SyncAsync` → Refit methods on `IPathApi` (POST endpoints).
- `ICaseRoomSyncReceiver` → subscribes to a new `CaseRoomSyncReceived` event on `SseClientService`, which dispatches `caseroom-sync` SSE events.

**Server implementation** (`InMemoryCaseRoomSyncService`):
- `JoinAsync` / `LeaveAsync` / `SyncAsync` → direct calls to `ICaseRoomSessionStore` (injected, same process).
- `ICaseRoomSyncReceiver` → subscribes to `INotificationEventBus.SubscribeCaseRoomSync(requestId, handler)`.

**Registration** (in `RazorLibServiceRegistration.AddRazorLibServices`):
```csharp
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

#### 3. API Endpoints — `CaseRoomEndpoints`

New static class `CaseRoomEndpoints` with `MapCaseRoomApi(this IEndpointRouteBuilder)`:

| Method | Path | Body | Returns | Notes |
|--------|------|------|---------|-------|
| POST | `/api/v1/caseroom/{requestId}/join` | none | `CaseRoomSnapshot` | Idempotent; first caller creates session |
| POST | `/api/v1/caseroom/{requestId}/leave` | none | 204 | Removes caller; 30s grace teardown |
| POST | `/api/v1/caseroom/{requestId}/sync` | `SyncPayload` | 204 | Server updates + broadcasts to others |
| GET | `/api/v1/caseroom/{requestId}` | none | `CaseRoomStatus?` | For badge: null if no session |

All endpoints require authorization (same `IUserSession` pattern as `NotificationEndpoints`).

#### 4. SSE Integration

**`SseConnectionManager`** already has `SendToUserAsync(Guid userId, string eventType, object payload)`. No change needed — the session store calls it with eventType `"caseroom-sync"`.

**`SseClientService`** gets one new event:
```csharp
public event EventHandler<CaseRoomSyncEvent>? CaseRoomSyncReceived;

[JSInvokable]
public void OnCaseRoomSync(string data, string lastEventId)
{
    var evt = JsonSerializer.Deserialize<CaseRoomSyncEvent>(data, ...);
    CaseRoomSyncReceived?.Invoke(this, evt);
}
```

The JS module (`ipath-sse.js`) already dispatches by event type — just add a `case` for `"caseroom-sync"`.

#### 5. `INotificationEventBus` extension

Add parallel methods to existing interface:
```csharp
void PublishCaseRoomSync(CaseRoomSyncEvent evt);
IDisposable SubscribeCaseRoomSync(Action<CaseRoomSyncEvent> handler);
```
Implementation mirrors `PublishDomainEvent` / `SubscribeDomainEvents`.

#### 6. Blazor Page — `CaseRoomPage.razor`

Route: `@page "/request/{id}/caseroom"`
Layout: `SlideshowLayout` (same fullscreen layout as `SlideShowPage`)

**Structure:**
- Full-screen OSD canvas (`<div id="osd-caseroom">`) — no iframe.
- Minimal overlay controls (top bar): prev/next, participants count, leave button.
- Participants sidebar (collapsible): list of who's in the room.

**Code-behind (`CaseRoomPage.razor.cs`):**
```csharp
public partial class CaseRoomPage : ComponentBase, IAsyncDisposable
{
    [Parameter] public string id { get; set; }
    [Inject] ICaseRoomSyncService SyncService { get; set; }
    [Inject] ICaseRoomSyncReceiver SyncReceiver { get; set; }
    [Inject] IJSRuntime JS { get; set; }
    [Inject] ServiceRequestViewModel vm { get; set; }

    private Guid RequestId => Guid.Parse(id);
    private IJSObjectReference? _module;
    private IDisposable? _syncSub;
    private bool _isApplyingRemoteSync;  // guard against echo loops

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await vm.LoadNode(RequestId);
            var snapshot = await SyncService.JoinAsync(RequestId);

            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/iPath.RazorLib/js/ipath-caseroom.js");

            _syncSub = SyncReceiver.Subscribe(RequestId, OnSyncReceived);

            // Initialize OSD with current active document (or first slide)
            var docId = snapshot.ActiveDocumentId ?? vm.SelectedRequest.Documents.FirstOrDefault(d => d.IsSlide)?.Id;
            if (docId.HasValue)
            {
                var doc = vm.SelectedRequest.Documents.First(d => d.Id == docId.Value);
                await _module.InvokeVoidAsync("initOsd", "osd-caseroom", GetTileSourceUrl(doc));
            }
        }
    }

    private async Task OnSyncReceived(CaseRoomSyncEvent evt)
    {
        _isApplyingRemoteSync = true;
        if (evt.Payload.DocumentId.HasValue)
        {
            // Switch to new document
            vm.SelectDocument(evt.Payload.DocumentId.Value);
            var doc = vm.SelectedDocument;
            await _module.InvokeVoidAsync("openTileSource", GetTileSourceUrl(doc));
        }
        if (evt.Payload.Viewport is not null)
        {
            await _module.InvokeVoidAsync("setViewport",
                evt.Payload.Viewport.X, evt.Payload.Viewport.Y, evt.Payload.Viewport.Zoom);
        }
        _isApplyingRemoteSync = false;
    }

    // Called from JS via [JSInvokable] when OSD viewport changes (throttled ~150ms)
    [JSInvokable]
    public async Task OnViewportChanged(double x, double y, double zoom)
    {
        if (_isApplyingRemoteSync) return;  // don't echo remote updates back
        await SyncService.SyncAsync(RequestId, new SyncPayload(null, new ViewportState(x, y, zoom)));
    }

    // Called from JS when user navigates to a different document
    [JSInvokable]
    public async Task OnDocumentChanged(Guid documentId)
    {
        if (_isApplyingRemoteSync) return;
        await SyncService.SyncAsync(RequestId, new SyncPayload(documentId, null));
    }
}
```

#### 7. JS Interop Module — `ipath-caseroom.js`

Located at `src/ui/iPath.RazorLib/wwwroot/js/ipath-caseroom.js`.

**Responsibilities:**
- `initOsd(divId, tileSourceUrl)`: creates OpenSeadragon viewer, attaches `viewport-change` handler with throttle (~150ms), calls `DotNet.invokeMethodAsync(component, "OnViewportChanged", x, y, zoom)`.
- `openTileSource(url)`: `viewer.open(url)` — switches to a new slide.
- `setViewport(x, y, zoom)`: `viewer.viewport.panTo({x, y}, true); viewer.viewport.zoomTo(zoom, null, true)` — applies remote update without triggering local sync (use a guard flag, not the OSD `immediately` param).
- `dispose()`: cleans up OSD instance and event handlers.

**Throttle implementation:**
```javascript
let throttleTimer = null;
viewer.addHandler('viewport-change', () => {
    if (isApplyingRemote) return;
    if (throttleTimer) return;
    throttleTimer = setTimeout(() => {
        const center = viewer.viewport.getCenter();
        const zoom = viewer.viewport.getZoom();
        dotNetRef.invokeMethodAsync('OnViewportChanged', center.x, center.y, zoom);
        throttleTimer = null;
    }, 150);
});
```

**Guard against echo:**
```javascript
let isApplyingRemote = false;
function setViewport(x, y, zoom) {
    isApplyingRemote = true;
    viewer.viewport.panTo({x: x, y: y}, true);
    viewer.viewport.zoomTo(zoom, null, true);
    // Release guard after a tick to let OSD's own viewport-change settle
    setTimeout(() => { isApplyingRemote = false; }, 50);
}
```

#### 8. "Room Active" Badge on ServiceRequest Page

`GET /api/v1/caseroom/{requestId}` returns `CaseRoomStatus?`. If non-null, the request list / detail page shows a badge "N viewing" with a link to `/request/{id}/caseroom`.

This is a lightweight polling check (infrequent, no SSE needed). Can be enhanced later with a push notification via the existing SSE event stream.

### File Layout (new files)

```
src/
├── core/iPath.Application/
│   └── Features/CaseRoom/
│       ├── CaseRoomModels.cs                 # ViewportState, Participant, SyncPayload, CaseRoomSnapshot, CaseRoomStatus, CaseRoomSyncEvent
│       ├── ICaseRoomSessionStore.cs          # Interface
│       └── CaseRoomSyncService.cs            # ICaseRoomSyncService, ICaseRoomSyncReceiver interfaces
│
├── infrastructure/iPath.API/
│   ├── Endpoints/
│   │   └── CaseRoomEndpoints.cs              # MapCaseRoomApi
│   └── Services/CaseRoom/
│       └── CaseRoomSessionStore.cs           # In-memory implementation
│
├── ui/
│   ├── iPath.RazorLib/
│   │   ├── ServiceRequests/
│   │   │   └── CaseRoomPage.razor            # + .cs code-behind
│   │   ├── wwwroot/js/
│   │   │   └── ipath-caseroom.js             # OSD init, interop, throttle
│   │   └── CaseRoom/                          # ViewModel, services registration
│   │       ├── CaseRoomViewModel.cs
│   │       ├── HttpCaseRoomSyncService.cs     # WASM implementation
│   │       ├── InMemoryCaseRoomSyncService.cs # Server implementation
│   │       └── CaseRoomSyncReceiver.cs        # Dual-mode receiver
│   │
│   ├── iPath.Blazor.ServiceLib/ApiClient/
│   │   └── IApiClient.cs                     # Add Refit methods (join/leave/sync/status)
│   └── iPath.Blazor.ServiceLib/Services/
│       └── DirectApiClient.cs                # Add direct mediator/handler calls
```

### Modifications to Existing Files

| File | Change |
|------|--------|
| `NotificationEventBus.cs` | Add `PublishCaseRoomSync` / `SubscribeCaseRoomSync` |
| `SseClientService.cs` | Add `CaseRoomSyncReceived` event + `[JSInvokable] OnCaseRoomSync` |
| `ipath-sse.js` | Add `caseroom-sync` event dispatch case |
| `IApiClient.cs` | Add Refit methods for join/leave/sync/status |
| `DirectApiClient.cs` | Add direct call implementations |
| `RazorLibServiceRegistration.cs` | Register `ICaseRoomSyncService` / `ICaseRoomSyncReceiver` based on `WasmClient` |
| `APIServicesRegistration.cs` | Register `ICaseRoomSessionStore` as singleton |
| `Program.cs` (Blazor.Server) | Call `MapCaseRoomApi` |
| `ServiceRequestPage.razor` | Add "CaseRoom active" badge with N participants |
| `_Imports.razor` (relevant folder) | Add `@using iPath.Blazor.Componenents.CaseRoom` if new namespace |

### Auth & Access Control

- All CaseRoom endpoints require authenticated user (same `IUserSession` pattern).
- `JoinAsync` checks that the user has access to the ServiceRequest's group (existing authorization — group membership). This can reuse the same authorization logic that gates `ServiceRequestPage`.
- No additional role checks for v1 — any group member can join and drive.

### Error Handling

- Session not found on `sync` or `leave`: return 404 (or silently no-op — join is idempotent so a re-join recovers).
- OSD load failure: show error overlay (reuse the loader pattern from `OsdViewerHtml.cs`).
- JS interop failure (circuit disconnect): `DisposeAsync` calls `SyncService.LeaveAsync`. If that fails (connection gone), the 30s teardown timer handles cleanup.

### Throttling & Performance

- **Viewport sync throttle: 150ms** on the JS side (client-side throttle before calling C#).
- **Server-side: no throttle needed** — session store just overwrites the current viewport. Broadcasts are cheap (~50 bytes per user).
- **Document change: no throttle** — infrequent, immediate broadcast.
- **Participant list updates**: broadcast on join/leave only. Not throttled.

### Testing

- **Unit tests** (xUnit + FluentAssertions, existing pattern):
  - `CaseRoomSessionStoreTests`: join creates session, second join adds participant, leave removes, teardown timer, sync updates viewport, sync changes document, GetStatus returns correct state.
  - `NotificationEventBusTests`: `PublishCaseRoomSync` / `SubscribeCaseRoomSync` round-trip.
- **Integration tests** (if existing test infra supports it):
  - `CaseRoomEndpoints`: join → sync → leave; unauthorized access rejected.
- **Manual testing**: open two browser windows (different users), navigate to `/request/{id}/caseroom`, verify synced pan/zoom and document switching.

### Out of Scope (Future)

- Chat / messages in the room
- Shared annotations/markers on the slide
- Real-time pointer/cursor overlay (telepointer)
- Presenter control model (raise hand, pass control)
- Multiple parallel rooms per case
- Persisted session history (LiveSession entity)
- SignalR transport implementation
- Reconnection state recovery (re-sync after network drop — current: re-join returns snapshot)

## Open Questions — Resolved

| Question | Answer |
|----------|--------|
| Where does it live? | Blazor page (Approach A) |
| Who's in control? | Shared board — any participant drives |
| What's synced? | Document selection + pan/zoom |
| Naming? | CaseRoom (avoids "theatre" medical conflict) |
| Session identity? | One room per ServiceRequestId |
| Persistence? | In-memory only for v1 |
| Transport? | Abstraction: REST+SSE (WASM), in-process (Server), SignalR (future) |
| OSD integration? | Inline JS interop, no iframe |