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

