## Commit list

555cd26 feat(caseroom): wire caseroom-sync SSE event through SseClientService

## Stat summary

 .../Notifications/SseClientService.cs              | 18 +++++++++++
 src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js      |  4 +++
 .../CaseRoom/CaseRoomSseClientTests.cs             | 37 ++++++++++++++++++++++
 test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj    |  1 +
 4 files changed, 60 insertions(+)

## Full diff

diff --git a/src/ui/iPath.RazorLib/Notifications/SseClientService.cs b/src/ui/iPath.RazorLib/Notifications/SseClientService.cs
index 508e847..875920f 100644
--- a/src/ui/iPath.RazorLib/Notifications/SseClientService.cs
+++ b/src/ui/iPath.RazorLib/Notifications/SseClientService.cs
@@ -1,10 +1,11 @@
+using iPath.Application.Features.CaseRoom;
 using iPath.Application.Features.Notifications;
 using Microsoft.Extensions.DependencyInjection;
 using Microsoft.JSInterop;
 using System.Text.Json;
 
 namespace iPath.Blazor.Componenents.Notifications;
 
 public class SseClientService : IAsyncDisposable
 {
     private readonly ILogger<SseClientService> _logger;
@@ -18,20 +19,21 @@ public class SseClientService : IAsyncDisposable
     // WASM mode: JS interop fields
     private IJSRuntime? _js;
     private IJSObjectReference? _module;
     private IJSObjectReference? _eventSource;
     private DotNetObjectReference<SseClientService>? _dotNetHelper;
     private string? _lastEventId;
 
     public event EventHandler<NotificationDto>? NotificationReceived;
     public event EventHandler<DomainEventSummary>? DomainEventReceived;
     public event EventHandler<SystemEventHint>? SystemEventReceived;
+    public event EventHandler<CaseRoomSyncEvent>? CaseRoomSyncReceived;
     public event EventHandler? ConnectionError;
 
     public SseClientService(IJSRuntime js, IServiceProvider serviceProvider, ILogger<SseClientService> logger)
     {
         _js = js;
         _serviceProvider = serviceProvider;
         _logger = logger;
         _isServerMode = !OperatingSystem.IsBrowser();
     }
 
@@ -129,20 +131,36 @@ public class SseClientService : IAsyncDisposable
             var hint = JsonSerializer.Deserialize<SystemEventHint>(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
             if (hint is not null)
                 SystemEventReceived?.Invoke(this, hint);
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Failed to deserialize system-event");
         }
     }
 
+    [JSInvokable]
+    public void OnCaseRoomSync(string data, string lastEventId)
+    {
+        _lastEventId = lastEventId;
+        try
+        {
+            var evt = JsonSerializer.Deserialize<CaseRoomSyncEvent>(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
+            if (evt is not null)
+                CaseRoomSyncReceived?.Invoke(this, evt);
+        }
+        catch (Exception ex)
+        {
+            _logger.LogError(ex, "Failed to deserialize caseroom-sync");
+        }
+    }
+
     [JSInvokable]
     public void OnError()
     {
         _logger.LogWarning("SSE connection error; will auto-reconnect");
         ConnectionError?.Invoke(this, EventArgs.Empty);
     }
 
     public async ValueTask DisposeAsync()
     {
         if (_isServerMode)
diff --git a/src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js b/src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js
index 8bb4983..e992617 100644
--- a/src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js
+++ b/src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js
@@ -6,16 +6,20 @@ export function connect(dotNetHelper, url) {
     });
 
     es.addEventListener('domain-event', e => {
         dotNetHelper.invokeMethodAsync('OnDomainEvent', e.data, e.lastEventId);
     });
 
     es.addEventListener('system-event', e => {
         dotNetHelper.invokeMethodAsync('OnSystemEvent', e.data, e.lastEventId);
     });
 
+    es.addEventListener('caseroom-sync', e => {
+        dotNetHelper.invokeMethodAsync('OnCaseRoomSync', e.data, e.lastEventId);
+    });
+
     es.onerror = () => {
         dotNetHelper.invokeMethodAsync('OnError');
     };
 
     return { close: () => es.close() };
 }
diff --git a/test/iPath.Test.xUnit2/CaseRoom/CaseRoomSseClientTests.cs b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomSseClientTests.cs
new file mode 100644
index 0000000..b7f6128
--- /dev/null
+++ b/test/iPath.Test.xUnit2/CaseRoom/CaseRoomSseClientTests.cs
@@ -0,0 +1,37 @@
+using iPath.Blazor.Componenents.Notifications;
+using iPath.Application.Features.CaseRoom;
+using iPath.Application.Features.Notifications;
+using Microsoft.Extensions.DependencyInjection;
+using Microsoft.Extensions.Logging;
+using Microsoft.JSInterop;
+using NSubstitute;
+using FluentAssertions;
+
+namespace iPath.Test.xUnit2.CaseRoom;
+
+public class CaseRoomSseClientTests
+{
+    [Fact]
+    public void OnCaseRoomSync_RaisesEventWithDeserializedPayload()
+    {
+        var services = new ServiceCollection().BuildServiceProvider();
+        var logger = new LoggerFactory().CreateLogger<SseClientService>();
+        var js = Substitute.For<IJSRuntime>();
+        var service = new SseClientService(js, services, logger);
+
+        var received = new List<CaseRoomSyncEvent>();
+        service.CaseRoomSyncReceived += (_, e) => received.Add(e);
+
+        var requestId = Guid.NewGuid();
+        var userId = Guid.NewGuid();
+        var payload = $"{{\"requestId\":\"{requestId}\",\"userId\":\"{userId}\",\"displayName\":\"Alice\",\"payload\":{{\"documentId\":null,\"viewport\":{{\"x\":0.5,\"y\":0.5,\"zoom\":2.0}}}},\"timestamp\":\"2026-06-27T12:00:00+00:00\"}}";
+
+        var lastEventId = DateTimeOffset.UtcNow.ToString("o");
+        service.OnCaseRoomSync(payload, lastEventId);
+
+        received.Should().ContainSingle();
+        received[0].RequestId.Should().Be(requestId);
+        received[0].DisplayName.Should().Be("Alice");
+        received[0].Payload.Viewport!.Zoom.Should().Be(2.0);
+    }
+}
diff --git a/test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj b/test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj
index 7b3676d..33766c8 100644
--- a/test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj
+++ b/test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj
@@ -40,20 +40,21 @@
     <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
       <PrivateAssets>all</PrivateAssets>
       <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
     </PackageReference>
   </ItemGroup>
 
   <ItemGroup>
     <ProjectReference Include="..\..\src\infrastructure\iPath.API\iPath.API.csproj" />
     <ProjectReference Include="..\..\src\infrastructure\iPath.Database.EFCore\iPath.Database.EFCore.csproj" />
     <ProjectReference Include="..\..\src\ui\iPath.Blazor.ServiceLib\iPath.Blazor.ServiceLib.csproj" />
+    <ProjectReference Include="..\..\src\ui\iPath.RazorLib\iPath.Blazor.Componenents.csproj" />
   </ItemGroup>
 
   <ItemGroup>
     <Using Include="Xunit" />
   </ItemGroup>
 
 
 	<ItemGroup>
 		<None Update="appsettings.json">
 			<CopyToOutputDirectory>Always</CopyToOutputDirectory>
