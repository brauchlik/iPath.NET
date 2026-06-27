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

