# SSE Real-Time Events — Sprint 2: UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the Blazor UI to the SSE backend stream, display real-time notifications via MudBlazor Snackbar, trigger view refreshes on domain events, and invalidate client caches on system events.

**Architecture:** A scoped `SseClientService` uses `IJSRuntime` to connect a JavaScript `EventSource` to the SSE endpoint. It exposes C# events for `notification`, `domain-event`, and `system-event`. A dedicated `SseConnectionHost` component lives inside `MainLayout.razor` (within `AuthorizeView`) and routes events to Snackbar, `AppState`, and view model caches.

**Tech Stack:** Blazor (Server + WASM), JavaScript `EventSource`, `IJSRuntime`, MudBlazor, Refit

---

## File Structure

| File | Responsibility |
|---|---|
| `src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js` | JavaScript module: creates `EventSource`, forwards events to .NET via `DotNetObjectReference` |
| `src/ui/iPath.RazorLib/Notifications/SseClientService.cs` | Scoped Blazor service: JS interop wrapper, parses SSE payloads, exposes C# events |
| `src/ui/iPath.RazorLib/Notifications/SseConnectionHost.razor` | Component placed in `MainLayout`: connects on auth, routes events to UI |
| `src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs` | Registers `SseClientService` as scoped |
| `src/ui/iPath.Blazor.Client/Layout/MainLayout.razor` | Hosts `SseConnectionHost` inside `AuthorizeView` |

---

### Task 1: Create JavaScript EventSource Module

**Files:**
- Create: `src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js`

- [ ] **Step 1: Write the JS module**

```javascript
export function connect(dotNetHelper, url, lastEventId) {
    const fullUrl = lastEventId ? `${url}?lastEventId=${encodeURIComponent(lastEventId)}` : url;
    const es = new EventSource(fullUrl, { withCredentials: true });

    es.addEventListener('notification', (e) => {
        dotNetHelper.invokeMethodAsync('OnNotification', e.data, e.lastEventId);
    });

    es.addEventListener('domain-event', (e) => {
        dotNetHelper.invokeMethodAsync('OnDomainEvent', e.data, e.lastEventId);
    });

    es.addEventListener('system-event', (e) => {
        dotNetHelper.invokeMethodAsync('OnSystemEvent', e.data, e.lastEventId);
    });

    es.onerror = (e) => {
        dotNetHelper.invokeMethodAsync('OnError');
    };

    return {
        close: () => es.close()
    };
}
```

- [ ] **Step 2: Verify the file path exists**

If `src/ui/iPath.RazorLib/wwwroot/js/` does not exist, create it.

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js
git commit -m "feat(sse-ui): add JavaScript EventSource interop module"
```

---

### Task 2: Create SseClientService

**Files:**
- Create: `src/ui/iPath.RazorLib/Notifications/SseClientService.cs`

- [ ] **Step 1: Write the service**

```csharp
using iPath.Application.Features.Notifications;
using Microsoft.JSInterop;
using System.Text.Json;

namespace iPath.Blazor.Componenents.Notifications;

public class SseClientService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly ILogger<SseClientService> _logger;
    private IJSObjectReference? _module;
    private IJSObjectReference? _eventSource;
    private DotNetObjectReference<SseClientService>? _dotNetHelper;
    private string? _lastEventId;

    public event EventHandler<NotificationDto>? OnNotification;
    public event EventHandler<DomainEventSummary>? OnDomainEvent;
    public event EventHandler<SystemEventHint>? OnSystemEvent;
    public event EventHandler? OnConnectionError;

    public SseClientService(IJSRuntime js, ILogger<SseClientService> logger)
    {
        _js = js;
        _logger = logger;
    }

    public async Task ConnectAsync(string url)
    {
        try
        {
            _module = await _js.InvokeAsync<IJSObjectReference>("import", "./_content/iPath.Blazor.Componenents/js/ipath-sse.js");
            _dotNetHelper = DotNetObjectReference.Create(this);
            _eventSource = await _module.InvokeAsync<IJSObjectReference>("connect", _dotNetHelper, url, _lastEventId);
            _logger.LogInformation("SSE connected to {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect SSE");
        }
    }

    [JSInvokable]
    public void OnNotification(string data, string lastEventId)
    {
        _lastEventId = lastEventId;
        try
        {
            var dto = JsonSerializer.Deserialize<NotificationDto>(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (dto is not null)
                OnNotification?.Invoke(this, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize notification");
        }
    }

    [JSInvokable]
    public void OnDomainEvent(string data, string lastEventId)
    {
        _lastEventId = lastEventId;
        try
        {
            var evt = JsonSerializer.Deserialize<DomainEventSummary>(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (evt is not null)
                OnDomainEvent?.Invoke(this, evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize domain-event");
        }
    }

    [JSInvokable]
    public void OnSystemEvent(string data, string lastEventId)
    {
        _lastEventId = lastEventId;
        try
        {
            var hint = JsonSerializer.Deserialize<SystemEventHint>(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (hint is not null)
                OnSystemEvent?.Invoke(this, hint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize system-event");
        }
    }

    [JSInvokable]
    public void OnError()
    {
        _logger.LogWarning("SSE connection error; will auto-reconnect");
        OnConnectionError?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_eventSource is not null)
                await _eventSource.InvokeVoidAsync("close");
        }
        catch (JSDisconnectedException)
        {
            // expected during circuit disconnect or page navigation
        }
        _dotNetHelper?.Dispose();
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
```

- [ ] **Step 2: Build RazorLib**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.RazorLib/Notifications/SseClientService.cs
git commit -m "feat(sse-ui): add SseClientService with JS interop and event parsing"
```

---

### Task 3: Register SseClientService

**Files:**
- Modify: `src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs`

- [ ] **Step 1: Add registration**

Add to `AddRazorLibServices`:

```csharp
services.AddScoped<SseClientService>();
```

Place it near the other scoped services (e.g., after `services.AddScoped<AppState>()`).

- [ ] **Step 2: Build**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs
git commit -m "feat(sse-ui): register SseClientService as scoped"
```

---

### Task 4: Create SseConnectionHost Component

**Files:**
- Create: `src/ui/iPath.RazorLib/Notifications/SseConnectionHost.razor`

- [ ] **Step 1: Write the component**

```razor
@inject SseClientService Sse
@inject ISnackbar Snackbar
@inject AppState AppState
@inject UserViewModel UserVm
@inject IStringLocalizer T
@inject ILogger<SseConnectionHost> Logger
@inject NavigationManager Nav
@implements IAsyncDisposable

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Sse.OnNotification += HandleNotification;
            Sse.OnDomainEvent += HandleDomainEvent;
            Sse.OnSystemEvent += HandleSystemEvent;
            Sse.OnConnectionError += HandleConnectionError;

            var baseUrl = Nav.BaseUri.TrimEnd('/');
            await Sse.ConnectAsync($"{baseUrl}/api/v1/events/stream");
        }
    }

    private void HandleNotification(object? sender, NotificationDto dto)
    {
        _ = InvokeAsync(() =>
        {
            var message = dto.EventType switch
            {
                eNodeNotificationType.NodePublished => T["A new case has been published"],
                eNodeNotificationType.NewAnnotation => T["A new annotation has been added"],
                _ => T["New notification"]
            };
            Snackbar.Add(message, Severity.Info);
            AppState.OnChange?.Invoke();
        });
    }

    private void HandleDomainEvent(object? sender, DomainEventSummary evt)
    {
        _ = InvokeAsync(() =>
        {
            var message = evt.EventType switch
            {
                "AnnotationAddedEvent" => T["New annotation on case"],
                "ServiceRequestPublishedEvent" => T["New case published"],
                _ => T["Case updated"]
            };
            Snackbar.Add(message, Severity.Info);
            AppState.OnChange?.Invoke();
        });
    }

    private void HandleSystemEvent(object? sender, SystemEventHint hint)
    {
        _ = InvokeAsync(async () =>
        {
            Logger.LogInformation("System event received: {Hint} {ObjectId}", hint.Hint, hint.ObjectId);

            if (hint.Hint is "user" or "group" or "community")
            {
                await AppState.ReloadSession();
            }
        });
    }

    private void HandleConnectionError(object? sender, EventArgs e)
    {
        Logger.LogWarning("SSE connection lost");
    }

    public async ValueTask DisposeAsync()
    {
        Sse.OnNotification -= HandleNotification;
        Sse.OnDomainEvent -= HandleDomainEvent;
        Sse.OnSystemEvent -= HandleSystemEvent;
        Sse.OnConnectionError -= HandleConnectionError;
        await Sse.DisposeAsync();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.RazorLib/Notifications/SseConnectionHost.razor
git commit -m "feat(sse-ui): add SseConnectionHost component for event routing"
```

---

### Task 5: Wire SseConnectionHost into MainLayout

**Files:**
- Modify: `src/ui/iPath.Blazor.Client/Layout/MainLayout.razor`

- [ ] **Step 1: Add the component inside AuthorizeView**

Inside the existing `AuthorizeView` in `MudAppBar` (or add a new one), place the host component. The simplest location is right after `MudAppBar` content so it renders for authenticated users:

```razor
<MudAppBar Elevation="1">
    <!-- existing content -->
    <MudHidden Breakpoint="Breakpoint.Xs">
        <AuthorizeView>
            <Authorized>
                <IPathAvatar />
            </Authorized>
        </AuthorizeView>
    </MudHidden>
</MudAppBar>

<AuthorizeView>
    <Authorized>
        <SseConnectionHost />
    </Authorized>
</AuthorizeView>
```

Add the `@using` directive at the top of the file:
```razor
@using iPath.Blazor.Componenents.Notifications
```

- [ ] **Step 2: Build the client project**

Run: `dotnet build src/ui/iPath.Blazor.Client/iPath.Blazor.Client.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.Blazor.Client/Layout/MainLayout.razor
git commit -m "feat(sse-ui): wire SseConnectionHost into MainLayout for authenticated users"
```

---

### Task 6: Ensure _Imports.razor includes Notifications namespace

**Files:**
- Modify: `src/ui/iPath.RazorLib/_Imports.razor`
- Modify: `src/ui/iPath.Blazor.Client/_Imports.razor`

- [ ] **Step 1: Add using to RazorLib _Imports**

Add to `src/ui/iPath.RazorLib/_Imports.razor`:
```razor
@using iPath.Blazor.Componenents.Notifications
```

- [ ] **Step 2: Verify Blazor.Client _Imports**

Ensure `src/ui/iPath.Blazor.Client/_Imports.razor` already has:
```razor
@using iPath.Blazor.Componenents
```
If the `Notifications` namespace is not resolving, add:
```razor
@using iPath.Blazor.Componenents.Notifications
```

- [ ] **Step 3: Build entire solution**

Run: `dotnet build`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(sse-ui): add Notifications namespace to _Imports.razor"
```

---

### Task 7: Manual End-to-End Verification

- [ ] **Step 1: Run the application**

Run: `dotnet run --project src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj`

- [ ] **Step 2: Open browser and log in**

Navigate to `https://localhost:5001` (or the URL shown in console), log in with a valid user.

- [ ] **Step 3: Open browser DevTools → Network → EventStream**

Look for a request to `/api/v1/events/stream`. It should show as pending (open connection).

- [ ] **Step 4: Trigger a domain event**

In another browser tab or via the API, create a new annotation on a service request in the same group as the logged-in user.

Verify:
- A `domain-event` appears in the EventStream tab
- A MudBlazor Snackbar appears in the UI

- [ ] **Step 5: Trigger a notification**

Ensure the logged-in user has InApp notifications enabled for the group. Create a new published service request.

Verify:
- A `notification` appears in the EventStream tab
- A Snackbar appears with "A new case has been published"

- [ ] **Step 6: Test reconnection**

Close the SSE connection (e.g., disable network briefly or restart the server). The browser should auto-reconnect. Check that `Last-Event-ID` is sent in the reconnect request.

- [ ] **Step 7: Commit verification notes (optional)**

If any fixes are needed, commit them.

---

## Self-Review Checklist

1. **Spec coverage:**
   - ✅ `SseClientService` with JS interop — Task 2
   - ✅ Event routing (notification → Snackbar) — Task 4
   - ✅ Event routing (domain-event → refresh) — Task 4
   - ✅ Event routing (system-event → cache invalidation) — Task 4
   - ✅ Reconnection with `Last-Event-ID` — Task 2 (JS passes it)
   - ✅ Service registration — Task 3
   - ✅ Layout integration — Task 5

2. **Placeholder scan:** No TBD/TODO found.

3. **Type consistency:**
   - `NotificationDto`, `DomainEventSummary`, `SystemEventHint` match Sprint 1 DTOs
   - JS event type names (`notification`, `domain-event`, `system-event`) match server SSE `event:` field
   - `_content/iPath.Blazor.Componenents/js/ipath-sse.js` path matches assembly name

---

*Sprint 2 plan complete. Depends on Sprint 1 backend being deployed.*
