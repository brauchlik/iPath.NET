# Real-Time Event & Notification Streaming — Design Spec

> **Date:** 2026-05-10  
> **Branch:** `feature/sse-realtime-events`  
> **Sprints:** 2 (Backend → UI)

---

## 1. Goal

Replace the experimental SignalR real-time layer with a robust **Server-Sent Events (SSE)** stream that delivers:

1. **In-app notifications** — per-user, subscription-filtered (existing notification pipeline, new delivery channel)
2. **Domain events** — lightweight summaries of `ServiceRequestEvent`s scoped to group membership
3. **System events** — minimal hints for non-ServiceRequest events that affect client-cached data (users, groups, communities)

The existing **email notification pipeline** is kept unchanged.

---

## 2. Architecture Overview

### 2.1 Current State (Baseline)

- Domain events (`ServiceRequestEvent`) are raised during command handlers and dispatched via **DispatchR**.
- `NotificationEventHandler` enqueues `ServiceRequestEvent`s into `IServiceRequestEventQueue` (in-memory Channel).
- `ServiceRequestEventProcessor` dequeues events, reads group subscriptions, filters via `INotificationFilterService`, and creates `Notification` DB records per target (`InApp`, `Email`).
- `NotificationPublisher` (BackgroundService) dequeues pending `Notification` records and routes to `INotificationPublisher` implementations. Currently only `EmailNotificationPublisher` is registered.
- An experimental `DomainEventSignalrProcessor` broadcasts all events unfiltered to a SignalR hub (`NodeNotificationsHub`). This is unreliable due to proxy/routing issues.

### 2.2 Target State

```
┌─────────────────────────────────────────────────────────────────────┐
│                         iPath.API (Server)                          │
├─────────────────────────────────────────────────────────────────────┤
│  Domain Events                                                      │
│  ┌──────────────┐   ┌──────────────────┐   ┌─────────────────────┐ │
  │  │ DispatchR    │──▶│ NotificationEvent│──▶│ ServiceRequestEvent │ │
  │  │ Pipeline     │   │ Handler          │   │ Queue (Channel)     │ │
│  └──────────────┘   └──────────────────┘   └─────────────────────┘ │
│         │                                                    │      │
│         │                    ┌───────────────────────────────┘      │
│         │                    ▼                                       │
│         │         ┌─────────────────────┐                            │
│         │         │ ServiceRequestEvent │                            │
│         │         │ Processor           │                            │
│         │         └─────────────────────┘                            │
│         │                    │                                       │
│         │      ┌─────────────┴─────────────┐                        │
│         │      ▼                           ▼                        │
│         │ ┌─────────────┐         ┌─────────────────┐               │
│         │ │ InApp       │         │ Email           │               │
│         │ │ Notification│         │ Notification    │               │
│         │ │ Enqueued    │         │ Enqueued        │               │
│         │ └─────────────┘         └─────────────────┘               │
│         │        │                                                  │
│         │        ▼                                                  │
│         │ ┌─────────────────────────────────────────────────────┐   │
│         │ │ NotificationPublisher (BackgroundService)           │   │
│         │ └─────────────────────────────────────────────────────┘   │
│         │        │                           │                      │
│         │        ▼                           ▼                      │
│         │ ┌─────────────────┐     ┌──────────────────────┐          │
│         │ │ INotification   │     │ INotification        │          │
│         │ │ Publisher       │     │ Publisher            │          │
│         │ │ (InApp)  ───────┼────▶│ SseConnectionManager │          │
│         │ └─────────────────┘     └──────────────────────┘          │
│         │                                                   │       │
│         │      ┌────────────────────────────────────────────┘       │
│         │      ▼                                                    │
│         │ ┌─────────────────────────────────────────────────────┐   │
  │         └▶│ MembershipEventBroadcaster (DispatchR handler)      │   │
│           │ - Listens to ALL ServiceRequestEvents                 │   │
│           │ - Filters by user's group membership                  │   │
│           │ - Pushes to SseConnectionManager                      │   │
│           └─────────────────────────────────────────────────────┘   │
│                                                                     │
│         ┌─────────────────────────────────────────────────────┐     │
  │         │ SystemEventBroadcaster (DispatchR handler)          │     │
│         │ - Listens to non-ServiceRequest Events                │     │
│         │ - Broadcasts to all connected clients                 │     │
│         │ - Pushes to SseConnectionManager                      │     │
│         └─────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
                           ┌─────────────────┐
                           │ SSE Endpoint    │
                           │ GET /api/v1/    │
                           │   events/stream │
                           │ (authenticated) │
                           └─────────────────┘
                                    │
                                    ▼
                           ┌─────────────────┐
                           │ Blazor Client   │
                           │ EventSource     │
                           └─────────────────┘
```

**Key principle:** Three independent publishers feed the same `SseConnectionManager`. The client receives all event types on a single SSE connection, distinguished by SSE `event:` field.

---

## 3. SSE Event Types

The single SSE stream multiplexes three event categories:

| SSE Event Type | Source | Server-Side Filter | Payload | Client Action |
|---|---|---|---|---|
| `notification` | `InAppNotificationPublisher` | Per-user subscription (existing `NotificationFilterService`) | Full `NotificationDto` | Toast / badge counter |
| `domain-event` | `MembershipEventBroadcaster` | Group membership (`UserSession` group cache) | Lightweight `DomainEventSummary` | Refresh service request list / annotation panel |
| `system-event` | `SystemEventBroadcaster` | All connected authenticated users | Minimal `SystemEventHint` | Invalidate client cache (user/groups/communities) and re-query |

### 3.1 Example SSE Stream

```http
id: 2026-05-10T14:30:00.000Z
event: domain-event
data: {"eventType":"AnnotationAddedEvent","eventId":"...","serviceRequestId":"...","groupId":"...","eventDate":"..."}

id: 2026-05-10T14:30:01.000Z
event: notification
data: {"id":"...","eventType":"NewAnnotation","date":"...","receiver":{...},"serviceRequestId":"..."}

id: 2026-05-10T14:30:02.000Z
event: system-event
data: {"eventType":"GroupUpdatedEvent","objectId":"...","hint":"group"}
```

---

## 4. Server Components

### 4.1 `ISseConnectionManager` / `SseConnectionManager` (Singleton)

Thread-safe manager of active SSE connections.

**Responsibilities:**
- Maintain `ConcurrentDictionary<Guid, List<SseConnection>>` keyed by `userId`
- Each connection gets a unique `connectionId` and a `Channel<SseMessage>`
- Write loop per connection reads from the channel and writes SSE-formatted bytes to `HttpResponse.Body`
- Auto-cleanup on disconnect or cancellation

**Key API:**

```csharp
public interface ISseConnectionManager
{
    Task AddConnectionAsync(Guid userId, HttpResponse response, CancellationToken ct);
    Task RemoveConnectionAsync(Guid userId, Guid connectionId);
    Task SendToUserAsync(Guid userId, string eventType, object payload);
    Task SendToGroupMembersAsync(Guid groupId, string eventType, object payload);
    Task BroadcastAsync(string eventType, object payload);
}
```

**Notes:**
- `SendToGroupMembersAsync` resolves group → users via the existing `UserSession` cached group membership. If cache miss, falls back to a lightweight DB query.
- SSE writes must use `await response.Body.WriteAsync(...)` with proper `text/event-stream` headers and flush.

### 4.2 SSE Endpoint

Replace the experimental `/food` endpoint.

```csharp
app.MapGet("api/v1/events/stream", async (
    [FromServices] ISseConnectionManager mgr,
    [FromServices] IUserSession sess,
    [FromServices] iPathDbContext db,
    [FromQuery] string? lastEventId, // ISO 8601 date for catch-up
    HttpContext ctx,
    CancellationToken ct) =>
{
    if (!ctx.User.Identity?.IsAuthenticated ?? true)
        return Results.Unauthorized();

    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";

    // Optional catch-up: query eventstore for EventDate > lastEventId
    // Emit missed domain-events and system-events in chronological order
    // Then hand over to connection manager for live events
    await mgr.AddConnectionAsync(sess.User!.Id, ctx.Response, ct);
    return Results.Empty;
});
```

**Authentication:** Standard cookie-based auth (same as existing API). The Blazor server forwards cookies via `ForwardCookiesHandler`.

### 4.3 `InAppNotificationPublisher`

New `INotificationPublisher` implementation registered alongside `EmailNotificationPublisher`.

```csharp
public class InAppNotificationPublisher(ISseConnectionManager sse, ILogger<InAppNotificationPublisher> logger)
    : INotificationPublisher
{
    public eNotificationTarget Target => eNotificationTarget.InApp;

    public async Task PublishAsync(Notification n, CancellationToken ct)
    {
        var dto = n.ToDto(); // existing extension
        await sse.SendToUserAsync(n.UserId, "notification", dto);
        n.MarkAsSent();
    }
}
```

**Registration in `APIServicesRegistration.cs`:**

```csharp
services.AddScoped<INotificationPublisher, EmailNotificationPublisher>();
services.AddScoped<INotificationPublisher, InAppNotificationPublisher>();
```

### 4.4 `MembershipEventBroadcaster`

New DispatchR `INotificationHandler<EventEntity>`.

**What it does:** Listens to all domain events dispatched through DispatchR. When it sees a `ServiceRequestEvent` (e.g., `AnnotationAddedEvent`, `ServiceRequestPublishedEvent`), it extracts the `GroupId` from the event's associated `ServiceRequest`, builds a lightweight `DomainEventSummary`, and pushes it via `SseConnectionManager` to **all currently connected SSE clients who are members of that group**.

**It does NOT create `Notification` DB records.** It bypasses the notification queue entirely. Its sole purpose is to send a "something happened in group X" live hint to connected browsers so the UI can refresh lists or show live indicators. This is separate from the `InAppNotificationPublisher`, which sends full per-user subscription-filtered notifications.

```csharp
public class MembershipEventBroadcaster(
    ISseConnectionManager sse,
    IUserSession sess, // used as a factory or via a lightweight group resolver
    ILogger<MembershipEventBroadcaster> logger)
    : INotificationHandler<EventEntity>
{
    public async ValueTask Handle(EventEntity evt, CancellationToken ct)
    {
        if (evt is not ServiceRequestEvent srEvt) return;

        var groupId = srEvt.ServiceRequest.GroupId;
        var summary = new DomainEventSummary(
            evt.EventName,
            evt.EventId,
            srEvt.ServiceRequest.Id,
            groupId,
            evt.EventDate);

        await sse.SendToGroupMembersAsync(groupId, "domain-event", summary);
    }
}
```

**DTO:**

```csharp
public record DomainEventSummary(
    string EventType,
    Guid EventId,
    Guid ServiceRequestId,
    Guid GroupId,
    DateTime EventDate);
```

### 4.5 `SystemEventBroadcaster`

New DispatchR `INotificationHandler<EventEntity>`.

```csharp
public class SystemEventBroadcaster(ISseConnectionManager sse, ILogger<SystemEventBroadcaster> logger)
    : INotificationHandler<EventEntity>
{
    public async ValueTask Handle(EventEntity evt, CancellationToken ct)
    {
        if (evt is ServiceRequestEvent) return; // handled by MembershipEventBroadcaster

        var hint = new SystemEventHint(evt.EventName, evt.ObjectId, DeriveHint(evt));
        await sse.BroadcastAsync("system-event", hint);
    }

    private static string DeriveHint(EventEntity evt) => evt.EventName switch
    {
        var n when n.Contains("Group") => "group",
        var n when n.Contains("Community") => "community",
        var n when n.Contains("User") => "user",
        _ => "system"
    };
}
```

**DTO:**

```csharp
public record SystemEventHint(string EventType, Guid ObjectId, string Hint);
```

### 4.6 Reconnection / Catch-Up

- SSE `id:` field = `EventDate.ToString("o")` (ISO 8601, sortable)
- Client reconnects automatically with `Last-Event-ID` header
- Server parses the date, queries `eventstore` for `EventDate > since`
- `EventDate` is already indexed in the database
- Missed `domain-event`s and `system-event`s are emitted in chronological order, then the connection switches to live
- **Notifications do NOT use SSE replay** — the client should call `GET /api/v1/notifications/list` on reconnect to catch up on missed notifications

---

## 5. Data Model Notes

### 5.1 `EventEntity` (Existing)

Stored in `eventstore` table:

| Column | Type | Indexed |
|---|---|---|
| `EventId` | Guid (PK) | Yes |
| `EventDate` | DateTime | Yes |
| `EventName` | string(100) | Yes |
| `ObjectId` | Guid | Yes |
| `ObjectName` | string(50) | Yes |
| `Payload` | string | No |
| `UserId` | Guid | No |

**Important:** `EventEntity` does **not** have a `GroupId` column. For `ServiceRequestEvent`, `GroupId` is accessed via `evt.ServiceRequest.GroupId`. For the real-time stream, we only broadcast `ServiceRequestEvent`s for group-scoped filtering.

### 5.2 `Notification` (Existing)

Already has `ServiceRequestId` and `EventId` FKs. No schema changes required for Sprint 1.

---

## 6. SignalR Cleanup

### 6.1 Remove or Deprecate

- `DomainEventSignalrProcessor` — remove entirely (unfiltered broadcast, experimental)
- `NodeNotificationsHub` — remove if no other consumers
- `SignalREndpoints.MapIPathHubs` — remove
- `TestEventHandler` — currently uses `IHubContext<NodeNotificationsHub>`; refactor to use `ISseConnectionManager.BroadcastAsync("system-event", ...)` or remove if no longer needed

### 6.2 Keep `AddSignalR()`?

**No.** `AddSignalR()` was only needed for the experimental `NodeNotificationsHub`. SSE works over standard HTTP and functions correctly in both **Blazor Server** (development) and **Blazor WebAssembly** (production) without any SignalR infrastructure. Remove `services.AddSignalR()` from `APIServicesRegistration.cs` entirely.

---

## 7. Security

- **Authentication:** SSE endpoint requires authenticated user. Returns `401 Unauthorized` otherwise.
- **Authorization (domain events):** Filtered by group membership. A user only receives `domain-event`s for groups they belong to (as resolved by `UserSession` cache).
- **Authorization (notifications):** Filtered by existing `INotificationFilterService` + per-user `NotificationQueue` records.
- **No sensitive data in lightweight summaries:** `DomainEventSummary` and `SystemEventHint` contain only IDs and event metadata. The client fetches full data via normal API calls.

---

## 8. Error Handling

- `SseConnectionManager` must catch and log per-connection write exceptions without crashing other connections.
- Disconnected clients are removed from the dictionary automatically (detected via `Channel` completion or `CancellationToken`).
- `InAppNotificationPublisher` marks notification as failed if SSE delivery fails after a short retry (or immediately, depending on policy).

---

## 9. Testing Strategy

### 9.1 Unit Tests

- `SseConnectionManager` — mock `HttpResponse`, verify channel write/read loop, verify `SendToGroupMembersAsync` resolves correct users.
- `InAppNotificationPublisher` — verify `MarkAsSent` called and `SendToUserAsync` invoked with correct payload.
- `MembershipEventBroadcaster` — verify only `ServiceRequestEvent`s trigger group-scoped sends.
- `SystemEventBroadcaster` — verify non-ServiceRequest events trigger broadcasts.

### 9.2 Integration Tests

- Connect two authenticated SSE clients, trigger a `ServiceRequestEvent`, verify only the group member receives it.
- Trigger a notification, verify the target user receives it via SSE.
- Disconnect and reconnect with `Last-Event-ID`, verify catch-up events are delivered.

---

## 10. Sprint Breakdown

### Sprint 1: Backend

**Scope:**
1. Create `ISseConnectionManager` and `SseConnectionManager`
2. Create `InAppNotificationPublisher` and register it
3. Create `MembershipEventBroadcaster`
4. Create `SystemEventBroadcaster`
5. Implement SSE endpoint with optional catch-up
6. Remove/replace SignalR experimental code
7. Unit + integration tests

**Deliverable:** Working SSE endpoint that authenticated clients can connect to. Events and notifications are streamed correctly.

### Sprint 2: UI

**Scope:**
1. Create `SseClientService` (Blazor scoped service)
2. Wire `EventSource` to SSE endpoint in `App.razor` or layout
3. Handle `notification` events → Snackbar + badge counter
4. Handle `domain-event` events → refresh service request lists / detail views
5. Handle `system-event` events → invalidate `UserViewModel` cache and re-query
6. Implement reconnection with `Last-Event-ID`
7. UI tests / manual verification

**Deliverable:** Blazor UI updates in real time without page refresh.

---

## 11. Open Questions (Resolved)

| Question | Decision |
|---|---|
| Single or multiple SSE connections? | **Single multiplexed connection** — simpler client-side |
| Scope of real-time events? | **Group membership** (Option A) — use `UserSession` cached groups |
| Incrementing EventId for stream recreation? | Use `EventDate` (ISO 8601) as SSE `id:` — already indexed; query `eventstore` on reconnect |
| Should non-ServiceRequest events be filtered? | No — broadcast as `system-event` to all clients; client invalidates cache |
| Keep SignalR? | **Remove** the experimental hub; remove `AddSignalR()` if no other hubs exist |

---

## 12. Files to Create / Modify (Sprint 1)

### Create
- `src/infrastructure/iPath.API/Services/Notifications/Publisher/InAppNotificationPublisher.cs`
- `src/infrastructure/iPath.API/Services/Notifications/SseConnectionManager.cs`
- `src/core/iPath.Application/Features/Notifications/SseMessageDto.cs` (or shared DTOs)
- `src/core/iPath.Application/Features/Notifications/DomainEventSummary.cs`
- `src/core/iPath.Application/Features/Notifications/SystemEventHint.cs`
- `src/infrastructure/iPath.API/EventHandlers/MembershipEventBroadcaster.cs`
- `src/infrastructure/iPath.API/EventHandlers/SystemEventBroadcaster.cs`
- `test/iPath.Test.xUnit2/Notifications/SseConnectionManagerTests.cs`
- `test/iPath.Test.xUnit2/Notifications/MembershipEventBroadcasterTests.cs`

### Modify
- `src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs` — replace `/food` with production SSE endpoint
- `src/infrastructure/iPath.API/APIServicesRegistration.cs` — register `InAppNotificationPublisher`, `SseConnectionManager`, remove SignalR hub if appropriate
- `src/infrastructure/iPath.API/EventHandlers/NodeEventHandler.cs` — remove `DomainEventSignalrProcessor`
- `src/infrastructure/iPath.API/Hubs/NodeNotificationsHub.cs` — remove (if unused)
- `src/infrastructure/iPath.API/Endpoints/SignalREndpoints.cs` — remove hub mapping
- `src/infrastructure/iPath.API/EventHandlers/TestEventHandler.cs` — refactor or remove SignalR usage
- `src/ui/iPath.Blazor.Server/Program.cs` — remove `AddSignalR()` if applicable, remove test `NotificationService` registration

---

*Spec written and committed. Proceed to Sprint 1 implementation plan upon approval.*
