# SSE Real-Time Events — Sprint 1: Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the server-side SSE infrastructure: connection manager, in-app notification publisher, domain/system event broadcasters, authenticated SSE endpoint, and SignalR cleanup.

**Architecture:** A singleton `SseConnectionManager` tracks active SSE channels per user. `InAppNotificationPublisher` sends subscription-filtered notifications via SSE. `MembershipEventBroadcaster` and `SystemEventBroadcaster` are DispatchR handlers that push lightweight event hints to connected clients. All traffic flows over one authenticated SSE endpoint.

**Tech Stack:** ASP.NET Core SSE, DispatchR, System.Threading.Channels, xUnit, FluentAssertions

---

## File Structure

| File | Responsibility |
|---|---|
| `src/core/iPath.Application/Features/Notifications/DomainEventSummary.cs` | Lightweight DTO for ServiceRequestEvent summaries |
| `src/core/iPath.Application/Features/Notifications/SystemEventHint.cs` | Minimal DTO for non-ServiceRequest system events |
| `src/core/iPath.Application/Features/Notifications/SseMessage.cs` | Internal SSE message shape (eventType, data, id) |
| `src/infrastructure/iPath.API/Services/Notifications/SseConnectionManager.cs` | Singleton: tracks connections, routes messages to users/groups/all |
| `src/infrastructure/iPath.API/Services/Notifications/Publisher/InAppNotificationPublisher.cs` | `INotificationPublisher` for `InApp` target |
| `src/infrastructure/iPath.API/EventHandlers/MembershipEventBroadcaster.cs` | DispatchR handler: group-scoped domain events → SSE |
| `src/infrastructure/iPath.API/EventHandlers/SystemEventBroadcaster.cs` | DispatchR handler: system events → SSE broadcast |
| `src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs` | Production SSE endpoint (replaces `/food`) |
| `test/iPath.Test.xUnit2/Notifications/SseConnectionManagerTests.cs` | Unit tests for connection manager |
| `test/iPath.Test.xUnit2/Notifications/InAppNotificationPublisherTests.cs` | Unit tests for in-app publisher |
| `test/iPath.Test.xUnit2/Notifications/MembershipEventBroadcasterTests.cs` | Unit tests for broadcasters |

---

### Task 1: Create DTOs

**Files:**
- Create: `src/core/iPath.Application/Features/Notifications/DomainEventSummary.cs`
- Create: `src/core/iPath.Application/Features/Notifications/SystemEventHint.cs`
- Create: `src/core/iPath.Application/Features/Notifications/SseMessage.cs`

- [ ] **Step 1: Create DomainEventSummary**

```csharp
namespace iPath.Application.Features.Notifications;

public record DomainEventSummary(
    string EventType,
    Guid EventId,
    Guid ServiceRequestId,
    Guid GroupId,
    DateTime EventDate);
```

- [ ] **Step 2: Create SystemEventHint**

```csharp
namespace iPath.Application.Features.Notifications;

public record SystemEventHint(string EventType, Guid ObjectId, string Hint);
```

- [ ] **Step 3: Create SseMessage**

```csharp
namespace iPath.Application.Features.Notifications;

public record SseMessage(string EventType, string Data, string? Id = null);
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/core/iPath.Application/iPath.Application.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/core/iPath.Application/Features/Notifications/
git commit -m "feat(sse): add DomainEventSummary, SystemEventHint, SseMessage DTOs"
```

---

### Task 2: Create SseConnectionManager

**Files:**
- Create: `src/infrastructure/iPath.API/Services/Notifications/SseConnectionManager.cs`

- [ ] **Step 1: Write the interface and implementation**

```csharp
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace iPath.API.Services.Notifications;

public interface ISseConnectionManager
{
    Task AddConnectionAsync(Guid userId, HttpResponse response, CancellationToken ct);
    Task SendToUserAsync(Guid userId, string eventType, object payload, string? id = null);
    Task SendToGroupMembersAsync(Guid groupId, string eventType, object payload, string? id = null);
    Task BroadcastAsync(string eventType, object payload, string? id = null);
}

public class SseConnectionManager(IServiceProvider services, ILogger<SseConnectionManager> logger)
    : ISseConnectionManager
{
    private readonly ConcurrentDictionary<Guid, List<SseConnection>> _connections = new();

    public async Task AddConnectionAsync(Guid userId, HttpResponse response, CancellationToken ct)
    {
        var connectionId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<SseMessage>();
        var connection = new SseConnection(connectionId, channel);

        _connections.AddOrUpdate(userId,
            _ => [connection],
            (_, list) => { list.Add(connection); return list; });

        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(ct))
            {
                await WriteMessageAsync(response, message, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("SSE connection {ConnectionId} for user {UserId} cancelled", connectionId, userId);
        }
        finally
        {
            RemoveConnection(userId, connectionId);
        }
    }

    public async Task SendToUserAsync(Guid userId, string eventType, object payload, string? id = null)
    {
        if (!_connections.TryGetValue(userId, out var connections)) return;

        var data = JsonSerializer.Serialize(payload);
        var message = new SseMessage(eventType, data, id);
        foreach (var conn in connections.ToList())
        {
            try { await conn.Channel.Writer.WriteAsync(message); }
            catch (ChannelClosedException) { /* connection closing */ }
        }
    }

    public async Task SendToGroupMembersAsync(Guid groupId, string eventType, object payload, string? id = null)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();

        var userIds = await db.Set<GroupMember>()
            .AsNoTracking()
            .Where(m => m.GroupId == groupId && m.Role != eMemberRole.Banned)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync();

        var data = JsonSerializer.Serialize(payload);
        var message = new SseMessage(eventType, data, id);
        foreach (var userId in userIds)
        {
            if (!_connections.TryGetValue(userId, out var connections)) continue;
            foreach (var conn in connections.ToList())
            {
                try { await conn.Channel.Writer.WriteAsync(message); }
                catch (ChannelClosedException) { /* connection closing */ }
            }
        }
    }

    public async Task BroadcastAsync(string eventType, object payload, string? id = null)
    {
        var data = JsonSerializer.Serialize(payload);
        var message = new SseMessage(eventType, data, id);
        foreach (var kvp in _connections.ToList())
        {
            foreach (var conn in kvp.Value.ToList())
            {
                try { await conn.Channel.Writer.WriteAsync(message); }
                catch (ChannelClosedException) { /* connection closing */ }
            }
        }
    }

    private void RemoveConnection(Guid userId, Guid connectionId)
    {
        SseConnection? toClose = null;
        _connections.AddOrUpdate(userId,
            _ => [],
            (_, list) =>
            {
                toClose = list.FirstOrDefault(c => c.ConnectionId == connectionId);
                list.RemoveAll(c => c.ConnectionId == connectionId);
                return list;
            });

        toClose?.Channel.Writer.Complete();

        if (_connections.TryGetValue(userId, out var remaining) && remaining.Count == 0)
            _connections.TryRemove(userId, out _);
    }

    private static async Task WriteMessageAsync(HttpResponse response, SseMessage message, CancellationToken ct)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(message.Id))
            sb.AppendLine($"id: {message.Id}");
        sb.AppendLine($"event: {message.EventType}");
        foreach (var line in message.Data.Split('\n'))
            sb.AppendLine($"data: {line}");
        sb.AppendLine();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await response.Body.WriteAsync(bytes, ct);
        await response.Body.FlushAsync(ct);
    }
}

public class SseConnection(Guid connectionId, Channel<SseMessage> channel)
{
    public Guid ConnectionId { get; } = connectionId;
    public Channel<SseMessage> Channel { get; } = channel;
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/infrastructure/iPath.API/iPath.API.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/infrastructure/iPath.API/Services/Notifications/SseConnectionManager.cs
git commit -m "feat(sse): add SseConnectionManager with user/group/broadcast routing"
```

---

### Task 3: Unit Test SseConnectionManager

**Files:**
- Create: `test/iPath.Test.xUnit2/Notifications/SseConnectionManagerTests.cs`

- [ ] **Step 1: Write test for SendToUserAsync**

```csharp
using iPath.API.Services.Notifications;
using Microsoft.AspNetCore.Http;

namespace iPath.Test.xUnit2.Notifications;

public class SseConnectionManagerTests
{
    [Fact]
    public async Task SendToUserAsync_DeliversMessageToConnectedUser()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseConnectionManager>();
        var mgr = new SseConnectionManager(services, logger);

        var userId = Guid.NewGuid();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        var cts = new CancellationTokenSource();

        var connectionTask = mgr.AddConnectionAsync(userId, ctx.Response, cts.Token);
        await mgr.SendToUserAsync(userId, "test-event", new { foo = "bar" });
        cts.Cancel();
        try { await connectionTask; } catch (OperationCanceledException) { }

        ctx.Response.Body.Position = 0;
        var text = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        text.Should().Contain("event: test-event");
        text.Should().Contain("data: {\"foo\":\"bar\"}");
    }

    [Fact]
    public async Task SendToUserAsync_DoesNotDeliverToOtherUser()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<SseConnectionManager>();
        var mgr = new SseConnectionManager(services, logger);

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var ctxA = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var ctxB = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var ctsA = new CancellationTokenSource();
        var ctsB = new CancellationTokenSource();

        var taskA = mgr.AddConnectionAsync(userA, ctxA.Response, ctsA.Token);
        var taskB = mgr.AddConnectionAsync(userB, ctxB.Response, ctsB.Token);

        await mgr.SendToUserAsync(userA, "test", new { msg = "hello" });

        ctsA.Cancel();
        ctsB.Cancel();
        try { await taskA; } catch (OperationCanceledException) { }
        try { await taskB; } catch (OperationCanceledException) { }

        ctxA.Response.Body.Position = 0;
        ctxB.Response.Body.Position = 0;
        var textA = await new StreamReader(ctxA.Response.Body).ReadToEndAsync();
        var textB = await new StreamReader(ctxB.Response.Body).ReadToEndAsync();

        textA.Should().Contain("data: {\"msg\":\"hello\"}");
        textB.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~SseConnectionManagerTests"`
Expected: 2 tests pass.

- [ ] **Step 3: Commit**

```bash
git add test/iPath.Test.xUnit2/Notifications/SseConnectionManagerTests.cs
git commit -m "test(sse): add SseConnectionManager unit tests"
```

---

### Task 4: Create InAppNotificationPublisher

**Files:**
- Create: `src/infrastructure/iPath.API/Services/Notifications/Publisher/InAppNotificationPublisher.cs`

- [ ] **Step 1: Write implementation**

```csharp
using iPath.Application.Features.Notifications;

namespace iPath.API.Services.Notifications.Publisher;

public class InAppNotificationPublisher(ISseConnectionManager sse, ILogger<InAppNotificationPublisher> logger)
    : INotificationPublisher
{
    public eNotificationTarget Target => eNotificationTarget.InApp;

    public async Task PublishAsync(Notification n, CancellationToken ct)
    {
        try
        {
            var dto = n.ToDto();
            await sse.SendToUserAsync(n.UserId, "notification", dto, n.CreatedOn.ToString("o"));
            n.MarkAsSent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send in-app notification {NotificationId} to user {UserId}", n.Id, n.UserId);
            n.MarkAsFailed(ex.Message);
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/infrastructure/iPath.API/iPath.API.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/infrastructure/iPath.API/Services/Notifications/Publisher/InAppNotificationPublisher.cs
git commit -m "feat(sse): add InAppNotificationPublisher"
```

---

### Task 5: Unit Test InAppNotificationPublisher

**Files:**
- Create: `test/iPath.Test.xUnit2/Notifications/InAppNotificationPublisherTests.cs`

- [ ] **Step 1: Write test**

```csharp
using iPath.API.Services.Notifications;
using iPath.API.Services.Notifications.Publisher;
using iPath.Domain.Entities;
using iPath.Domain.Notifications;
using NSubstitute;

namespace iPath.Test.xUnit2.Notifications;

public class InAppNotificationPublisherTests
{
    [Fact]
    public async Task PublishAsync_ShouldMarkAsSent_WhenDeliverySucceeds()
    {
        var sse = Substitute.For<ISseConnectionManager>();
        var logger = new LoggerFactory().CreateLogger<InAppNotificationPublisher>();
        var pub = new InAppNotificationPublisher(sse, logger);

        var user = new User { Id = Guid.NewGuid(), UserName = "test", Email = "t@test.com", EmailConfirmed = true };
        var n = Notification.Create(eNodeNotificationType.NewAnnotation, eNotificationTarget.InApp, false, user.Id, Guid.NewGuid(), Guid.NewGuid());
        n.GetType().GetProperty("User")!.SetValue(n, user);

        await pub.PublishAsync(n, default);

        await sse.Received().SendToUserAsync(user.Id, "notification", Arg.Any<NotificationDto>(), Arg.Any<string>());
        n.Status.Should().Be(NotificationStatus.Sent);
    }
}
```

- [ ] **Step 2: Run test**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~InAppNotificationPublisherTests"`
Expected: 1 test passes.

- [ ] **Step 3: Commit**

```bash
git add test/iPath.Test.xUnit2/Notifications/InAppNotificationPublisherTests.cs
git commit -m "test(sse): add InAppNotificationPublisher unit test"
```

---

### Task 6: Create MembershipEventBroadcaster

**Files:**
- Create: `src/infrastructure/iPath.API/EventHandlers/MembershipEventBroadcaster.cs`

- [ ] **Step 1: Write implementation**

```csharp
using DispatchR.Abstractions.Notification;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;

namespace iPath.API.EventHandlers;

public class MembershipEventBroadcaster(ISseConnectionManager sse, ILogger<MembershipEventBroadcaster> logger)
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

        await sse.SendToGroupMembersAsync(groupId, "domain-event", summary, evt.EventDate.ToString("o"));
        logger.LogDebug("Broadcast domain-event {EventName} for group {GroupId}", evt.EventName, groupId);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/infrastructure/iPath.API/iPath.API.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/infrastructure/iPath.API/EventHandlers/MembershipEventBroadcaster.cs
git commit -m "feat(sse): add MembershipEventBroadcaster for group-scoped domain events"
```

---

### Task 7: Create SystemEventBroadcaster

**Files:**
- Create: `src/infrastructure/iPath.API/EventHandlers/SystemEventBroadcaster.cs`

- [ ] **Step 1: Write implementation**

```csharp
using DispatchR.Abstractions.Notification;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;

namespace iPath.API.EventHandlers;

public class SystemEventBroadcaster(ISseConnectionManager sse, ILogger<SystemEventBroadcaster> logger)
    : INotificationHandler<EventEntity>
{
    public async ValueTask Handle(EventEntity evt, CancellationToken ct)
    {
        if (evt is ServiceRequestEvent) return;

        var hint = new SystemEventHint(evt.EventName, evt.ObjectId, DeriveHint(evt));
        await sse.BroadcastAsync("system-event", hint, evt.EventDate.ToString("o"));
        logger.LogDebug("Broadcast system-event {EventName}", evt.EventName);
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

- [ ] **Step 2: Build**

Run: `dotnet build src/infrastructure/iPath.API/iPath.API.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/infrastructure/iPath.API/EventHandlers/SystemEventBroadcaster.cs
git commit -m "feat(sse): add SystemEventBroadcaster for system-wide cache invalidation hints"
```

---

### Task 8: Unit Test Broadcasters

**Files:**
- Create: `test/iPath.Test.xUnit2/Notifications/MembershipEventBroadcasterTests.cs`

- [ ] **Step 1: Write tests**

```csharp
using iPath.API.EventHandlers;
using iPath.API.Services.Notifications;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;
using NSubstitute;

namespace iPath.Test.xUnit2.Notifications;

public class MembershipEventBroadcasterTests
{
    [Fact]
    public async Task Handle_ServiceRequestEvent_SendsGroupScopedDomainEvent()
    {
        var sse = Substitute.For<ISseConnectionManager>();
        var logger = new LoggerFactory().CreateLogger<MembershipEventBroadcaster>();
        var handler = new MembershipEventBroadcaster(sse, logger);

        var groupId = Guid.NewGuid();
        var sr = new ServiceRequest { Id = Guid.NewGuid(), GroupId = groupId };
        var evt = new AnnotationAddedEvent { ServiceRequest = sr, EventId = Guid.NewGuid(), EventDate = DateTime.UtcNow, EventName = "AnnotationAddedEvent" };

        await handler.Handle(evt, default);

        await sse.Received().SendToGroupMembersAsync(
            groupId,
            "domain-event",
            Arg.Is<DomainEventSummary>(s => s.EventType == "AnnotationAddedEvent" && s.GroupId == groupId),
            Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_NonServiceRequestEvent_DoesNothing()
    {
        var sse = Substitute.For<ISseConnectionManager>();
        var logger = new LoggerFactory().CreateLogger<MembershipEventBroadcaster>();
        var handler = new MembershipEventBroadcaster(sse, logger);

        var evt = new GroupCreatedEvent { EventId = Guid.NewGuid(), EventDate = DateTime.UtcNow, EventName = "GroupCreatedEvent" };

        await handler.Handle(evt, default);

        await sse.DidNotReceive().SendToGroupMembersAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<string>());
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~MembershipEventBroadcasterTests"`
Expected: 2 tests pass.

- [ ] **Step 3: Commit**

```bash
git add test/iPath.Test.xUnit2/Notifications/MembershipEventBroadcasterTests.cs
git commit -m "test(sse): add MembershipEventBroadcaster unit tests"
```

---

### Task 9: Implement SSE Endpoint

**Files:**
- Modify: `src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs`

- [ ] **Step 1: Replace the experimental endpoint with the production SSE endpoint**

```csharp
using iPath.API.Services.Notifications;
using iPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace iPath.API;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationApi(this IEndpointRouteBuilder route)
    {
        route.MapGet("api/v1/events/stream", async (
            [FromServices] ISseConnectionManager mgr,
            [FromServices] IUserSession sess,
            [FromServices] iPathDbContext db,
            [FromQuery] string? lastEventId,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (sess.User is null)
                return Results.Unauthorized();

            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

            // Optional catch-up: emit missed events since lastEventId
            if (!string.IsNullOrEmpty(lastEventId)
                && DateTime.TryParse(lastEventId, null, DateTimeStyles.RoundtripKind, out var since))
            {
                var missed = await db.Set<EventEntity>()
                    .AsNoTracking()
                    .Where(e => e.EventDate > since)
                    .OrderBy(e => e.EventDate)
                    .ToListAsync(ct);

                foreach (var evt in missed)
                {
                    var id = evt.EventDate.ToString("o");
                    if (evt is ServiceRequestEvent srEvt)
                    {
                        var summary = new DomainEventSummary(
                            evt.EventName, evt.EventId, srEvt.ServiceRequest.Id,
                            srEvt.ServiceRequest.GroupId, evt.EventDate);
                        await sse.SendToUserAsync(sess.User.Id, "domain-event", summary, id);
                    }
                    else
                    {
                        var hint = new SystemEventHint(evt.EventName, evt.ObjectId, "system");
                        await sse.SendToUserAsync(sess.User.Id, "system-event", hint, id);
                    }
                }
            }

            await mgr.AddConnectionAsync(sess.User.Id, ctx.Response, ct);
            return Results.Empty;
        })
        .WithTags("Notifications")
        .RequireAuthorization();

        return route;
    }
}
```

- [ ] **Step 2: Remove old test files**

Delete the old `NotificationService` class and `myevent` record from the same file.

- [ ] **Step 3: Build**

Run: `dotnet build src/infrastructure/iPath.API/iPath.API.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs
git commit -m "feat(sse): implement authenticated SSE endpoint with optional catch-up"
```

---

### Task 10: Register Services and Cleanup SignalR

**Files:**
- Modify: `src/infrastructure/iPath.API/APIServicesRegistration.cs`
- Modify: `src/ui/iPath.Blazor.Server/Program.cs`
- Delete: `src/infrastructure/iPath.API/EventHandlers/NodeEventHandler.cs`
- Delete: `src/infrastructure/iPath.API/Hubs/NodeNotificationsHub.cs`
- Delete: `src/infrastructure/iPath.API/Endpoints/SignalREndpoints.cs`
- Modify: `src/infrastructure/iPath.API/EventHandlers/TestEventHandler.cs`

- [ ] **Step 1: Register new services in APIServicesRegistration.cs**

Find the notification handling section (~line 84-93) and update:

```csharp
// Notification Handling
services.AddSingleton<INotificationQueue>(ctx => new NotificationQueue(100));
services.AddScoped<INotificationFilterService>(ctx =>
    new NotificationFilterService(ctx.GetRequiredKeyedService<CodingService>("icdo")));
services.AddHostedService<Services.Notifications.ServiceRequestEventProcessor>();
services.AddScoped<IServiceRequestEventProcessor, Services.Notifications.Processors.ServiceRequestEventProcessor>();

// Publishers: Email + InApp (SSE)
services.AddScoped<INotificationPublisher, EmailNotificationPublisher>();
services.AddScoped<INotificationPublisher, InAppNotificationPublisher>();

// SSE Connection Manager (singleton)
services.AddSingleton<ISseConnectionManager, SseConnectionManager>();

services.AddHostedService<NotificationPublisher>();
services.AddTransient<IServiceRequestHtmlPreview, EmailNotificationPreview>();
```

Also remove `services.AddSignalR();` (~line 154-155).

- [ ] **Step 2: Remove SignalR endpoint mapping from MapEndpoints.cs**

In `src/infrastructure/iPath.API/MapEndpoints.cs`, remove `.MapIPathHubs()` from the chain (~line 35).

- [ ] **Step 3: Delete SignalR files**

Delete:
- `src/infrastructure/iPath.API/EventHandlers/NodeEventHandler.cs`
- `src/infrastructure/iPath.API/Hubs/NodeNotificationsHub.cs`
- `src/infrastructure/iPath.API/Endpoints/SignalREndpoints.cs`

- [ ] **Step 4: Refactor TestEventHandler**

In `src/infrastructure/iPath.API/EventHandlers/TestEventHandler.cs`, replace SignalR dependency with SSE broadcast:

```csharp
using DispatchR.Abstractions.Notification;
using iPath.API.Services.Notifications;

namespace iPath.API.EventHandlers;

public class TestEventHandler(ISseConnectionManager sse, IUserSession sess)
    : INotificationHandler<TestEvent>
{
    public async ValueTask Handle(TestEvent request, CancellationToken cancellationToken)
    {
        if (sess.User is not null)
        {
            await sse.BroadcastAsync("system-event", new { message = request.Message, user = sess.User.Username });
        }
    }
}
```

- [ ] **Step 5: Remove test NotificationService from Program.cs**

In `src/ui/iPath.Blazor.Server/Program.cs`, remove:
```csharp
// testing SSE
builder.Services.AddSingleton<NotificationService>();
```

Also remove `services.AddSignalR();` if still present.

- [ ] **Step 6: Build entire solution**

Run: `dotnet build`
Expected: Build succeeds with 0 errors.

- [ ] **Step 7: Run all tests**

Run: `dotnet test`
Expected: All existing tests pass; new tests pass.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(sse): register SSE services, remove SignalR experimental code"
```

---

## Self-Review Checklist

1. **Spec coverage:**
   - ✅ `SseConnectionManager` — Task 2
   - ✅ `InAppNotificationPublisher` — Task 4
   - ✅ `MembershipEventBroadcaster` — Task 6
   - ✅ `SystemEventBroadcaster` — Task 7
   - ✅ SSE endpoint — Task 9
   - ✅ SignalR cleanup — Task 10
   - ✅ Tests — Tasks 3, 5, 8

2. **Placeholder scan:** No TBD/TODO/fill-in-later found.

3. **Type consistency:**
   - `ISseConnectionManager` methods match usage in publishers and broadcasters
   - `SseMessage` constructor matches `new SseMessage(eventType, data, id)` pattern
   - DTOs (`DomainEventSummary`, `SystemEventHint`) match broadcaster creation sites

### Task 11: Add Notification Read Status

**Files:**
- Modify: `src/core/iPath.Domain/Entities/Notifications/Notification.cs`
- Modify: `src/core/iPath.Application/Features/Notifications/NotificationDto.cs`
- Modify: `src/infrastructure/iPath.Database.EFCore/Database/Configurations/NotificationConfiguration.cs` (if explicit config exists)

- [ ] **Step 1: Add ReadOn to Notification entity**

In `Notification.cs`, add after `ProcessedOn`:

```csharp
public DateTime? ReadOn { get; private set; }

public Notification MarkAsRead()
{
    ReadOn = DateTime.UtcNow;
    return this;
}
```

- [ ] **Step 2: Add ReadOn to NotificationDto**

In `NotificationDto.cs`, add `DateTime? ReadOn` parameter:

```csharp
public record NotificationDto(
    Guid Id,
    [property: SortBy("Date", "CreatedOn")] DateTime Date,
    eNodeNotificationType EventType,
    eNotificationTarget Target,
    [property: SortBy("Receiver.Username", "User.Username")] OwnerDto Receiver,
    Guid? ServiceRequestId = null,
    Guid? EventId = null,
    string? Payload = null,
    DateTime? ReadOn = null);
```

- [ ] **Step 3: Update ToDto extension**

In `NotificationExtensions.cs` (or wherever `ToDto()` is defined), map `ReadOn`:

```csharp
public static NotificationDto ToDto(this Notification n)
{
    return new NotificationDto(
        n.Id,
        n.CreatedOn,
        n.EventType,
        n.Target,
        new OwnerDto(n.UserId, n.User?.UserName, n.User?.Profile?.Initials),
        n.ServiceRequestId,
        n.EventId,
        n.Data,
        n.ReadOn);
}
```

- [ ] **Step 4: Create EF migration**

Run: `dotnet ef migrations add Notification_ReadOn --project src/infrastructure/iPath.Database.EFCore/iPath.Database.EFCore.csproj --startup-project src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj`

- [ ] **Step 5: Build and test**

Run: `dotnet build && dotnet test`
Expected: Build succeeds; all tests pass.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(notifications): add ReadOn to Notification entity and DTO"
```

---

### Task 12: Add Mark-As-Read API Endpoints

**Files:**
- Modify: `src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs`
- Modify: `src/core/iPath.Application/Features/Notifications/INotificationRepository.cs`
- Modify: `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Notifications/NotificationRepository.cs`
- Modify: `src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs`

- [ ] **Step 1: Add repository methods**

In `INotificationRepository.cs`:

```csharp
Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct);
Task MarkAllAsReadAsync(Guid userId, CancellationToken ct);
Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct);
```

In `NotificationRepository.cs`:

```csharp
public async Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct)
{
    var n = await db.NotificationQueue
        .Where(x => x.Id == notificationId && x.UserId == userId)
        .FirstOrDefaultAsync(ct);
    if (n is not null && !n.ReadOn.HasValue)
    {
        n.MarkAsRead();
        await db.SaveChangesAsync(ct);
    }
}

public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct)
{
    var unread = await db.NotificationQueue
        .Where(x => x.UserId == userId && x.ReadOn == null && x.Status == NotificationStatus.Sent)
        .ToListAsync(ct);
    foreach (var n in unread)
        n.MarkAsRead();
    await db.SaveChangesAsync(ct);
}

public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct)
{
    return await db.NotificationQueue
        .Where(x => x.UserId == userId && x.ReadOn == null && x.Status == NotificationStatus.Sent)
        .CountAsync(ct);
}
```

- [ ] **Step 2: Add endpoints**

In `NotificationEndpoints.cs` (same file as SSE endpoint):

```csharp
route.MapPut("api/v1/notifications/{id}/read", async (
    string id,
    [FromServices] INotificationRepository repo,
    [FromServices] IUserSession sess,
    CancellationToken ct) =>
{
    if (sess.User is null) return Results.Unauthorized();
    await repo.MarkAsReadAsync(Guid.Parse(id), sess.User.Id, ct);
    return Results.Ok();
})
.WithTags("Notifications")
.RequireAuthorization();

route.MapPut("api/v1/notifications/read-all", async (
    [FromServices] INotificationRepository repo,
    [FromServices] IUserSession sess,
    CancellationToken ct) =>
{
    if (sess.User is null) return Results.Unauthorized();
    await repo.MarkAllAsReadAsync(sess.User.Id, ct);
    return Results.Ok();
})
.WithTags("Notifications")
.RequireAuthorization();

route.MapGet("api/v1/notifications/unread-count", async (
    [FromServices] INotificationRepository repo,
    [FromServices] IUserSession sess,
    CancellationToken ct) =>
{
    if (sess.User is null) return Results.Unauthorized();
    var count = await repo.GetUnreadCountAsync(sess.User.Id, ct);
    return Results.Ok(count);
})
.WithTags("Notifications")
.RequireAuthorization();
```

- [ ] **Step 3: Add Refit methods to IApiClient**

In `IPathApi`:

```csharp
[Put("/api/v1/notifications/{id}/read")]
Task<IApiResponse> MarkNotificationAsRead(Guid id);

[Put("/api/v1/notifications/read-all")]
Task<IApiResponse> MarkAllNotificationsAsRead();

[Get("/api/v1/notifications/unread-count")]
Task<IApiResponse<int>> GetUnreadNotificationCount();
```

- [ ] **Step 4: Build and test**

Run: `dotnet build && dotnet test`
Expected: Build succeeds; all tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(notifications): add mark-as-read endpoints and unread count"
```

---

## Self-Review Checklist (Updated)

1. **Spec coverage:**
   - ✅ `SseConnectionManager` — Task 2
   - ✅ `InAppNotificationPublisher` — Task 4
   - ✅ `MembershipEventBroadcaster` — Task 6
   - ✅ `SystemEventBroadcaster` — Task 7
   - ✅ SSE endpoint — Task 9
   - ✅ SignalR cleanup — Task 10
   - ✅ Tests — Tasks 3, 5, 8
   - ✅ Notification ReadOn — Task 11
   - ✅ Mark-as-read endpoints — Task 12

2. **Placeholder scan:** No TBD/TODO/fill-in-later found.

3. **Type consistency:**
   - `ISseConnectionManager` methods match usage in publishers and broadcasters
   - `SseMessage` constructor matches `new SseMessage(eventType, data, id)` pattern
   - DTOs (`DomainEventSummary`, `SystemEventHint`) match broadcaster creation sites
   - `NotificationDto` includes `ReadOn` matching entity

---

*Sprint 1 plan complete. Proceed to Sprint 2 (UI) implementation plan next.*
