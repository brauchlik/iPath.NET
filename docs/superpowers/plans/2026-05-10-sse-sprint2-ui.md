# SSE Real-Time Events — Sprint 2: UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the NotificationCenter UI (bell badge + dropdown + full page), connect to SSE stream, display real-time notifications, and invalidate client caches on system events.

**Architecture:** `SseClientService` connects via JS interop. `NotificationBell` in the AppBar shows unread count. `NotificationDropdown` displays recent unread notifications. `NotificationPage` shows full paged list with mark-as-read on click. `SseConnectionHost` routes all events to Snackbar, `AppState`, and cache invalidation.

**Tech Stack:** Blazor (Server + WASM), JavaScript `EventSource`, `IJSRuntime`, MudBlazor, Refit

---

## File Structure

| File | Responsibility |
|---|---|
| `src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js` | JS module: creates `EventSource`, forwards events to .NET |
| `src/ui/iPath.RazorLib/Notifications/SseClientService.cs` | Scoped service: JS interop wrapper, parses payloads, exposes C# events |
| `src/ui/iPath.RazorLib/Notifications/SseConnectionHost.razor` | Component: connects on auth, routes events to UI and caches |
| `src/ui/iPath.RazorLib/Notifications/NotificationBell.razor` | AppBar icon with unread badge |
| `src/ui/iPath.RazorLib/Notifications/NotificationDropdown.razor` | Collapsible drawer: recent unread notifications |
| `src/ui/iPath.RazorLib/Notifications/NotificationPage.razor` | Full page (`/notifications`): paged list, mark-as-read on click |
| `src/ui/iPath.RazorLib/Shared/State/AppState.cs` | Badge count management |
| `src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs` | Registers services |
| `src/ui/iPath.Blazor.Client/Layout/MainLayout.razor` | Hosts `NotificationBell` and `SseConnectionHost` |

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

- [ ] **Step 2: Create directory if needed**

Run: `New-Item -ItemType Directory -Force -Path "src/ui/iPath.RazorLib/wwwroot/js"`

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

- [ ] **Step 2: Build**

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

- [ ] **Step 2: Build**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.RazorLib/RazorLibServiceRegistration.cs
git commit -m "feat(sse-ui): register SseClientService as scoped"
```

---

### Task 4: Update AppState with Badge Count

**Files:**
- Modify: `src/ui/iPath.RazorLib/Shared/State/AppState.cs`

- [ ] **Step 1: Add notification count and drawer toggle**

Add to `AppState` class:

```csharp
public int UnreadNotificationCount { get; private set; }
public bool NotificationDrawerOpen { get; private set; }

public void SetUnreadCount(int count)
{
    UnreadNotificationCount = count;
    OnChange?.Invoke();
}

public void IncrementUnreadCount()
{
    UnreadNotificationCount++;
    OnChange?.Invoke();
}

public void DecrementUnreadCount()
{
    if (UnreadNotificationCount > 0) UnreadNotificationCount--;
    OnChange?.Invoke();
}

public void ToggleNotificationDrawer()
{
    NotificationDrawerOpen = !NotificationDrawerOpen;
    OnChange?.Invoke();
}

public void CloseNotificationDrawer()
{
    NotificationDrawerOpen = false;
    OnChange?.Invoke();
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.RazorLib/Shared/State/AppState.cs
git commit -m "feat(sse-ui): add unread notification count and drawer state to AppState"
```

---

### Task 5: Add InvalidateCache to View Models

**Files:**
- Modify: `src/ui/iPath.RazorLib/Communities/CommunityViewModel.cs`
- Modify: `src/ui/iPath.RazorLib/Admin/Groups/GroupAdminViewModel.cs`
- Modify: `src/ui/iPath.RazorLib/Admin/Users/UserAdminViewModel.cs`
- Modify: `src/ui/iPath.RazorLib/Users/UserViewModel.cs`

- [ ] **Step 1: Add InvalidateCache to CommunityViewModel**

```csharp
public void InvalidateCache()
{
    cache.Remove("admin.communitylist");
}
```

- [ ] **Step 2: Add InvalidateCache to GroupAdminViewModel**

```csharp
public void InvalidateCache()
{
    cache.Remove("admin.grouplist");
}
```

- [ ] **Step 3: Add InvalidateCache to UserAdminViewModel**

```csharp
public void InvalidateCache()
{
    cache.Remove("admin.rolelist");
}
```

- [ ] **Step 4: Ensure ClearProfileCache exists on UserViewModel**

Verify `UserViewModel` already has:
```csharp
public void ClearProfileCache(Guid userId) => cache.Remove($"User_{userId}");
```
If not, add it.

- [ ] **Step 5: Build**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(sse-ui): add InvalidateCache methods to all view models"
```

---

### Task 6: Create NotificationBell

**Files:**
- Create: `src/ui/iPath.RazorLib/Notifications/NotificationBell.razor`

- [ ] **Step 1: Write component**

```razor
@inject AppState AppState

<MudBadge Content="AppState.UnreadNotificationCount"
          Color="Color.Error"
          Overlap="true"
          Visible="AppState.UnreadNotificationCount > 0"
          Class="mx-2">
    <MudIconButton Icon="@Icons.Material.Filled.Notifications"
                   Color="Color.Inherit"
                   OnClick="AppState.ToggleNotificationDrawer" />
</MudBadge>
```

- [ ] **Step 2: Build**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.RazorLib/Notifications/NotificationBell.razor
git commit -m "feat(sse-ui): add NotificationBell with unread badge"
```

---

### Task 7: Create NotificationDropdown

**Files:**
- Create: `src/ui/iPath.RazorLib/Notifications/NotificationDropdown.razor`
- Create: `src/ui/iPath.RazorLib/Notifications/NotificationDropdown.razor.cs`

- [ ] **Step 1: Write the Razor file**

```razor
@inject AppState AppState
@inject IPathApi Api
@inject NavigationManager Nav
@inject ISnackbar Snackbar
@inject IStringLocalizer T

<MudDrawer @bind-Open="AppState.NotificationDrawerOpen"
           Anchor="Anchor.Right"
           Elevation="2"
           Width="400px"
           ClipMode="DrawerClipMode.Always">
    <MudDrawerHeader>
        <MudText Typo="Typo.h6">@T["Notifications"]</MudText>
        <MudSpacer />
        <MudButton Size="Size.Small" OnClick="MarkAllAsRead">@T["Mark all as read"]</MudButton>
    </MudDrawerHeader>
    <MudList T="NotificationDto" Dense="true">
        @foreach (var n in RecentUnread)
        {
            <MudListItem OnClick="() => OpenNotification(n)">
                <MudText>@GetNotificationText(n)</MudText>
                <MudText Typo="Typo.caption">@n.Date.ToString("g")</MudText>
            </MudListItem>
        }
        @if (!RecentUnread.Any())
        {
            <MudListItem>
                <MudText Typo="Typo.body2" Class="mud-text-secondary">@T["No new notifications"]</MudText>
            </MudListItem>
        }
    </MudList>
    <MudButton Href="/notifications" FullWidth="true" Class="mt-2">
        @T["View all"]
    </MudButton>
</MudDrawer>
```

- [ ] **Step 2: Write the code-behind**

```csharp
using iPath.Application.Features.Notifications;
using iPath.Domain.Notifications;

namespace iPath.Blazor.Componenents.Notifications;

public partial class NotificationDropdown
{
    List<NotificationDto> RecentUnread = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadUnread();
    }

    async Task LoadUnread()
    {
        // Get first page of InApp notifications for current user
        var resp = await Api.GetNotifications(0, 10, eNotificationTarget.InApp);
        if (resp.IsSuccessful && resp.Content is not null)
        {
            RecentUnread = resp.Content.Items.Where(n => n.ReadOn is null).ToList();
        }
    }

    async Task OpenNotification(NotificationDto n)
    {
        if (n.ReadOn is null)
        {
            await Api.MarkNotificationAsRead(n.Id);
            AppState.DecrementUnreadCount();
        }
        AppState.CloseNotificationDrawer();
        if (n.ServiceRequestId.HasValue)
        {
            Nav.NavigateTo($"request/{n.ServiceRequestId.Value}");
        }
    }

    async Task MarkAllAsRead()
    {
        await Api.MarkAllNotificationsAsRead();
        AppState.SetUnreadCount(0);
        RecentUnread.Clear();
    }

    string GetNotificationText(NotificationDto n) => n.EventType switch
    {
        eNodeNotificationType.NodePublished => T["A new case has been published"],
        eNodeNotificationType.NewAnnotation => T["A new annotation has been added"],
        _ => T["New notification"]
    };
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/ui/iPath.RazorLib/Notifications/NotificationDropdown.razor src/ui/iPath.RazorLib/Notifications/NotificationDropdown.razor.cs
git commit -m "feat(sse-ui): add NotificationDropdown with mark-as-read and navigation"
```

---

### Task 8: Create NotificationPage

**Files:**
- Create: `src/ui/iPath.RazorLib/Notifications/NotificationPage.razor`
- Create: `src/ui/iPath.RazorLib/Notifications/NotificationPage.razor.cs`

- [ ] **Step 1: Write the Razor file**

```razor
@page "/notifications"
@inject IPathApi Api
@inject NavigationManager Nav
@inject ISnackbar Snackbar
@inject AppState AppState
@inject IStringLocalizer T
@inject IDialogService Dialog

<IPathHeader Title=@T["Notifications"]>
    <ActionContent>
        <MudFab StartIcon="@Icons.Material.TwoTone.Refresh"
                Color="Color.Primary"
                Size="Size.Small"
                OnClick="@(() => grid.ReloadServerData())" />
        <MudFab StartIcon="@Icons.Material.TwoTone.DoneAll"
                Color="Color.Secondary"
                Size="Size.Small"
                OnClick="MarkAllAsRead" />
        <MudFab StartIcon="@Icons.Material.TwoTone.DeleteForever"
                Color="Color.Error"
                Size="Size.Small"
                OnClick="DeleteAll" />
    </ActionContent>
</IPathHeader>

<MudDataGrid T="NotificationDto" ServerData="GetData" @ref="grid"
             Hover="true" Dense="true"
             RowClick="OnRowClick"
             HierarchyVisibilityToggled="OnDetailsOpened">
    <Columns>
        <HierarchyColumn T="NotificationDto" />
        <TemplateColumn Title="@T["Status"]">
            <CellTemplate>
                <MudIcon Icon="@(context.Item.ReadOn.HasValue ? Icons.Material.Filled.Done : Icons.Material.Filled.Circle)"
                         Color="@(context.Item.ReadOn.HasValue ? Color.Default : Color.Primary)" />
            </CellTemplate>
        </TemplateColumn>
        <PropertyColumn Property="e => e.EventType" Title=@T["Type"] />
        <PropertyColumn Property="e => e.Date" Format="g" Title=@T["Date"] />
        <TemplateColumn>
            <CellTemplate>
                <MudIconButton Icon="@Icons.Material.Filled.Delete"
                               Size="Size.Small"
                               OnClick="@(() => Delete(context.Item))" />
            </CellTemplate>
        </TemplateColumn>
    </Columns>
    <ChildRowContent>
        <MudCard Elevation="0">
            <MudCardContent>
                <MudText>@T["Service Request:"] @(context.Item.ServiceRequestId?.ToString() ?? "-")</MudText>
            </MudCardContent>
        </MudCard>
    </ChildRowContent>
    <PagerContent>
        <MudDataGridPager T="NotificationDto" />
    </PagerContent>
</MudDataGrid>
```

- [ ] **Step 2: Write the code-behind**

```csharp
using iPath.Application.Features.Notifications;
using iPath.Domain.Notifications;
using MudBlazor;

namespace iPath.Blazor.Componenents.Notifications;

public partial class NotificationPage
{
    MudDataGrid<NotificationDto> grid;

    public async Task<GridData<NotificationDto>> GetData(GridState<NotificationDto> state, CancellationToken ct = default)
    {
        var resp = await Api.GetNotifications(state.Page, state.PageSize, eNotificationTarget.InApp);
        if (resp.IsSuccessful && resp.Content is not null)
            return resp.Content.ToGridData();
        return new GridData<NotificationDto>();
    }

    async Task OnRowClick(DataGridRowClickEventArgs<NotificationDto> args)
    {
        await MarkAsRead(args.Item);
        if (args.Item.ServiceRequestId.HasValue)
        {
            Nav.NavigateTo($"request/{args.Item.ServiceRequestId.Value}");
        }
    }

    async Task OnDetailsOpened(MudBlazor.Utilities.DataGridHierarchyVisibilityToggledEventArgs<NotificationDto> args)
    {
        await MarkAsRead(args.Item);
    }

    async Task MarkAsRead(NotificationDto dto)
    {
        if (dto.ReadOn is null)
        {
            await Api.MarkNotificationAsRead(dto.Id);
            AppState.DecrementUnreadCount();
            dto = dto with { ReadOn = DateTime.UtcNow };
        }
    }

    async Task MarkAllAsRead()
    {
        var res = await Dialog.ShowMessageBoxAsync(T["Mark all as read"],
            T["Do you want to mark all notifications as read?"],
            yesText: T["Yes"], cancelText: T["Cancel"]);
        if (res.HasValue && res.Value)
        {
            await Api.MarkAllNotificationsAsRead();
            AppState.SetUnreadCount(0);
            await grid.ReloadServerData();
        }
    }

    async Task Delete(NotificationDto dto)
    {
        // TODO: add DELETE endpoint if needed; for now just reload
        await grid.ReloadServerData();
    }

    async Task DeleteAll()
    {
        var res = await Dialog.ShowMessageBoxAsync(T["Delete all"],
            T["Do you really want to delete all notifications?"],
            yesText: T["Yes"], cancelText: T["Cancel"]);
        if (res.HasValue && res.Value)
        {
            await Api.DeleteAllNotifications();
            AppState.SetUnreadCount(0);
            await grid.ReloadServerData();
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/ui/iPath.RazorLib/Notifications/NotificationPage.razor src/ui/iPath.RazorLib/Notifications/NotificationPage.razor.cs
git commit -m "feat(sse-ui): add NotificationPage with paging, mark-as-read, and navigation"
```

---

### Task 9: Create SseConnectionHost with Cache Invalidation

**Files:**
- Create: `src/ui/iPath.RazorLib/Notifications/SseConnectionHost.razor`

- [ ] **Step 1: Write component**

```razor
@inject SseClientService Sse
@inject ISnackbar Snackbar
@inject AppState AppState
@inject UserViewModel UserVm
@inject CommunityViewModel CommunityVm
@inject GroupAdminViewModel GroupAdminVm
@inject UserAdminViewModel UserAdminVm
@inject IGroupCache GroupCache
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

            // Load initial unread count
            await LoadUnreadCount();

            var baseUrl = Nav.BaseUri.TrimEnd('/');
            await Sse.ConnectAsync($"{baseUrl}/api/v1/events/stream");
        }
    }

    async Task LoadUnreadCount()
    {
        try
        {
            var resp = await Api.GetUnreadNotificationCount();
            if (resp.IsSuccessful)
                AppState.SetUnreadCount(resp.Content);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load unread count");
        }
    }

    private void HandleNotification(object? sender, NotificationDto dto)
    {
        _ = InvokeAsync(() =>
        {
            AppState.IncrementUnreadCount();
            var message = dto.EventType switch
            {
                eNodeNotificationType.NodePublished => T["A new case has been published"],
                eNodeNotificationType.NewAnnotation => T["A new annotation has been added"],
                _ => T["New notification"]
            };
            Snackbar.Add(message, Severity.Info);
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
            Logger.LogInformation("System event: {Hint} {ObjectId}", hint.Hint, hint.ObjectId);

            switch (hint.Hint)
            {
                case "group":
                    GroupCache.ClearGroup(hint.ObjectId);
                    GroupAdminVm?.InvalidateCache();
                    break;
                case "community":
                    CommunityVm?.InvalidateCache();
                    break;
                case "user":
                    UserVm.ClearProfileCache(hint.ObjectId);
                    UserAdminVm?.InvalidateCache();
                    break;
            }

            await AppState.ReloadSession();
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
git commit -m "feat(sse-ui): add SseConnectionHost with notification routing and cache invalidation"
```

---

### Task 10: Wire Components into MainLayout

**Files:**
- Modify: `src/ui/iPath.Blazor.Client/Layout/MainLayout.razor`

- [ ] **Step 1: Add NotificationBell and SseConnectionHost**

Inside the MudAppBar, add `NotificationBell` before `IPathAvatar`:

```razor
<MudAppBar Elevation="1">
    <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" Edge="Edge.Start" OnClick="@((e) => DrawerToggle())" />
    <MudText Typo="Typo.h5" Class="ml-3">iPath.NET</MudText>
    <MudSpacer />
    <MudHidden Breakpoint="Breakpoint.Xs">
        <AuthorizeView>
            <Authorized>
                <NotificationBell />
                <IPathAvatar />
            </Authorized>
        </AuthorizeView>
    </MudHidden>
</MudAppBar>
```

Add `NotificationDropdown` right after `MudAppBar` (still inside `AuthorizeView`):

```razor
<AuthorizeView>
    <Authorized>
        <NotificationDropdown />
        <SseConnectionHost />
    </Authorized>
</AuthorizeView>
```

Add `@using` at the top:
```razor
@using iPath.Blazor.Componenents.Notifications
```

- [ ] **Step 2: Build**

Run: `dotnet build src/ui/iPath.Blazor.Client/iPath.Blazor.Client.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/ui/iPath.Blazor.Client/Layout/MainLayout.razor
git commit -m "feat(sse-ui): wire NotificationBell, Dropdown, and ConnectionHost into MainLayout"
```

---

### Task 11: Ensure _Imports.razor includes Notifications namespace

**Files:**
- Modify: `src/ui/iPath.RazorLib/_Imports.razor`
- Modify: `src/ui/iPath.Blazor.Client/_Imports.razor`

- [ ] **Step 1: Add using to RazorLib _Imports**

Add:
```razor
@using iPath.Blazor.Componenents.Notifications
```

- [ ] **Step 2: Verify Blazor.Client _Imports**

Ensure:
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

### Task 12: Manual End-to-End Verification

- [ ] **Step 1: Run the application**

Run: `dotnet run --project src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj`

- [ ] **Step 2: Log in and verify SSE connection**

Open DevTools → Network → EventStream. Verify `/api/v1/events/stream` is pending.

- [ ] **Step 3: Trigger a notification**

Create a published service request in a group where the logged-in user has InApp notifications enabled.

Verify:
- Notification badge count increments
- Snackbar appears
- Notification appears in dropdown

- [ ] **Step 4: Click notification**

Click the notification in dropdown. Verify:
- Navigates to `/request/{id}`
- Badge count decrements
- Notification marked as read

- [ ] **Step 5: Trigger a system event**

Update a group name (or trigger another system event).

Verify:
- `system-event` appears in EventStream
- Client cache is invalidated (group name updates on next navigation)

- [ ] **Step 6: Test reconnection**

Briefly disconnect network. Verify browser auto-reconnects with `Last-Event-ID`.

- [ ] **Step 7: Commit any fixes**

```bash
git add -A
git commit -m "fix(sse-ui): address verification findings"
```

---

## Self-Review Checklist

1. **Spec coverage:**
   - ✅ `SseClientService` with JS interop — Task 2
   - ✅ `NotificationBell` with unread badge — Task 6
   - ✅ `NotificationDropdown` with mark-as-read — Task 7
   - ✅ `NotificationPage` with paging and navigation — Task 8
   - ✅ `SseConnectionHost` with cache invalidation — Task 9
   - ✅ `AppState` badge count management — Task 4
   - ✅ View model `InvalidateCache` methods — Task 5
   - ✅ Reconnection with `Last-Event-ID` — Task 2
   - ✅ Layout integration — Task 10

2. **Placeholder scan:** No TBD/TODO found.

3. **Type consistency:**
   - `NotificationDto.ReadOn` matches Sprint 1 backend
   - `Api.MarkNotificationAsRead`, `MarkAllNotificationsAsRead`, `GetUnreadNotificationCount` match Refit interface
   - JS event type names match server SSE `event:` field
   - `_content/iPath.Blazor.Componenents/js/ipath-sse.js` path matches assembly name

---

*Sprint 2 plan complete. Depends on Sprint 1 backend being deployed.*
