# SSE Notification System Fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 8 critical and medium issues in the SSE notification system and verify manually.

**Architecture:** Two complementary SSE paths — broadcasters (`domain-event`/`system-event`) are silent real-time signals (no snackbar), notification pipeline (`notification`) handles badge + snackbar + dropdown. Plus route fix, delete endpoint, heartbeat, Last-Event-ID support.

**Tech Stack:** .NET 10, EF Core, Blazor Server, MudBlazor, DispatchR, Refit

---

### Task 1: Fix SSE Route Double Prefix

**Files:**
- Modify: `src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs:14`

- [ ] **Change route prefix**

```csharp
// OLD:
route.MapGet("api/v1/events/stream", ...

// NEW:
route.MapGet("events/stream", ...
```

- [ ] **Build to verify**

```bash
dotnet build src/infrastructure/iPath.API/iPath.API.csproj
```

- [ ] **Commit**

```bash
git add src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs
git commit -m "fix: remove double api/v1 prefix on SSE stream route"
```

---

### Task 2: Add Delete Notification Endpoint

**Files:**
- Modify: `src/core/iPath.Application/Features/Notifications/INotificationRepository.cs`
- Modify: `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Notifications/NotificationRepository.cs`
- Modify: `src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs`
- Modify: `src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs`
- Modify: `src/ui/iPath.RazorLib/Notifications/NotificationPage.razor.cs`

- [ ] **Add Delete method to repository interface**

`INotificationRepository.cs`:
```csharp
Task Delete(Guid id, Guid userId, CancellationToken ct);
```

- [ ] **Implement Delete in repository**

`NotificationRepository.cs`:
```csharp
public async Task Delete(Guid id, Guid userId, CancellationToken ct)
{
    await db.NotificationQueue
        .Where(n => n.Id == id && n.UserId == userId)
        .ExecuteDeleteAsync(ct);
}
```

- [ ] **Add DELETE endpoint**

`NotificationEndpoints.cs` — add after the read-all endpoint:
```csharp
route.MapDelete("notifications/{id:guid}", async (Guid id, HttpContext ctx, [FromServices] INotificationRepository repo, [FromServices] IUserSession sess, CancellationToken ct) =>
{
    if (sess.User?.Id is null) return Results.Unauthorized();
    await repo.Delete(id, sess.User.Id.Value, ct);
    return Results.Ok();
}).RequireAuthorization();
```

- [ ] **Add Refit client method**

`IApiClient.cs`:
```csharp
[Delete("/api/v1/notifications/{id}")]
Task<IApiResponse> DeleteNotification(Guid id);
```

- [ ] **Wire up Delete in NotificationPage**

`NotificationPage.razor.cs` — replace the `Delete()` method:
```csharp
public async Task Delete(NotificationDto n)
{
    await api.DeleteNotification(n.Id);
    if (n.ReadOn is null)
        AppState.DecrementUnreadCount();
    await grid.ReloadServerData();
}
```

- [ ] **Build to verify**

```bash
dotnet build
```

- [ ] **Commit**

```bash
git add src/core/iPath.Application/Features/Notifications/INotificationRepository.cs src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Notifications/NotificationRepository.cs src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs src/ui/iPath.RazorLib/Notifications/NotificationPage.razor.cs
git commit -m "feat: add delete single notification endpoint and wire up UI"
```

---

### Task 3: Remove domain-event Snackbar (Complementary Dual Delivery)

**Files:**
- Modify: `src/ui/iPath.RazorLib/Notifications/SseConnectionHost.razor`

- [ ] **Remove snackbar from HandleDomainEvent**

`SseConnectionHost.razor` — in `HandleDomainEvent`, remove the `Snackbar.Add(...)` block. Keep only the `AppState.OnChange?.Invoke()` call:

```razor
private void HandleDomainEvent(DomainEventSummary evt)
{
    AppState.OnChange?.Invoke();
}
```

- [ ] **Build to verify**

```bash
dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj
```

- [ ] **Commit**

```bash
git add src/ui/iPath.RazorLib/Notifications/SseConnectionHost.razor
git commit -m "fix: remove domain-event snackbar to eliminate duplicate notifications; keep OnChange for live-UI signals"
```

---

### Task 4: Null Guard in MembershipEventBroadcaster

**Files:**
- Modify: `src/infrastructure/iPath.API/EventHandlers/MembershipEventBroadcaster.cs`

- [ ] **Add null check on ServiceRequest**

`MembershipEventBroadcaster.cs` — around the `GroupId` access:
```csharp
public async Task Handle(EventEntity notification, CancellationToken ct)
{
    if (notification is ServiceRequestEvent srEvt)
    {
        if (srEvt.ServiceRequest is null)
        {
            _logger.LogWarning("ServiceRequestEvent {EventId} has null ServiceRequest navigation property; skipping broadcast", srEvt.Id);
            return;
        }
        var evt = new DomainEventSummary(srEvt.EventName, srEvt.Id, srEvt.ServiceRequest.Id, srEvt.ServiceRequest.GroupId, srEvt.EventDate);
        await _sse.SendToGroupMembersAsync(srEvt.ServiceRequest.GroupId, "domain-event", evt, srEvt.Id.ToString());
    }
}
```

- [ ] **Build to verify**

```bash
dotnet build src/infrastructure/iPath.API/iPath.API.csproj
```

- [ ] **Commit**

```bash
git add src/infrastructure/iPath.API/EventHandlers/MembershipEventBroadcaster.cs
git commit -m "fix: add null guard on ServiceRequest navigation property in MembershipEventBroadcaster"
```

---

### Task 5: Bell Dropdown Auto-Refresh

**Files:**
- Modify: `src/ui/iPath.RazorLib/Notifications/NotificationBell.razor.cs`

- [ ] **Subscribe to SSE notification events in NotificationBell**

`NotificationBell.razor.cs` — add event subscription pattern (SseClientService.NotificationReceived is a C# event, not IObservable):

```csharp
protected override async Task OnInitializedAsync()
{
    await LoadUnread();
    if (Sse is not null)
        Sse.NotificationReceived += OnNotificationReceived;
}

private async void OnNotificationReceived(NotificationDto dto)
{
    await InvokeAsync(async () =>
    {
        await LoadUnread();
        StateHasChanged();
    });
}

public void Dispose()
{
    if (Sse is not null)
        Sse.NotificationReceived -= OnNotificationReceived;
}
```

- [ ] **Build to verify**

```bash
dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj
```

- [ ] **Commit**

```bash
git add src/ui/iPath.RazorLib/Notifications/NotificationBell.razor.cs
git commit -m "feat: auto-refresh bell dropdown on new SSE notification"
```

---

### Task 6: Notification Page Auto-Refresh

**Files:**
- Modify: `src/ui/iPath.RazorLib/Notifications/NotificationPage.razor.cs`

- [ ] **Subscribe to SSE notification events in NotificationPage**

`NotificationPage.razor.cs` — add event subscription:
```csharp
protected override async Task OnInitializedAsync()
{
    if (Sse is not null)
        Sse.NotificationReceived += OnNotificationReceived;
}

private async void OnNotificationReceived(NotificationDto dto)
{
    await InvokeAsync(async () =>
    {
        if (grid is not null)
            await grid.ReloadServerData();
    });
}

public void Dispose()
{
    if (Sse is not null)
        Sse.NotificationReceived -= OnNotificationReceived;
}
```

- [ ] **Build to verify**

```bash
dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj
```

- [ ] **Commit**

```bash
git add src/ui/iPath.RazorLib/Notifications/NotificationPage.razor.cs
git commit -m "feat: auto-refresh notification page grid on new SSE notification"
```

---

### Task 7: Add SSE Heartbeat

**Files:**
- Modify: `src/infrastructure/iPath.API/Services/Notifications/SseConnectionManager.cs`

- [ ] **Add PeriodicTimer for keepalive**

`SseConnectionManager.cs` — add timer field and lifecycle. In the existing class, add:

```csharp
private PeriodicTimer? _keepAliveTimer;
private CancellationTokenSource? _keepAliveCts;

// In AddConnectionAsync — start timer on first connection:
if (_connections.Count == 1)
    StartKeepAlive();

// New methods:
private void StartKeepAlive()
{
    _keepAliveCts = new CancellationTokenSource();
    _keepAliveTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    _ = KeepAliveLoopAsync(_keepAliveCts.Token);
}

private async Task KeepAliveLoopAsync(CancellationToken ct)
{
    try
    {
        while (await _keepAliveTimer!.WaitForNextTickAsync(ct))
        {
            await BroadcastAsync(": keepalive", null, null);
        }
    }
    catch (OperationCanceledException) { }
}

// In RemoveConnection — stop timer on last connection:
if (_connections.Count == 0)
    StopKeepAlive();

private void StopKeepAlive()
{
    _keepAliveCts?.Cancel();
    _keepAliveTimer?.Dispose();
    _keepAliveCts?.Dispose();
}
```

Handle null eventType in `WriteMessageAsync` — the keepalive comment `: keepalive\n\n` is written as an SSE comment. This may need a special case in the write method to output just `: keepalive\n\n` without the `event:` and `data:` lines.

- [ ] **Build to verify**

```bash
dotnet build src/infrastructure/iPath.API/iPath.API.csproj
```

- [ ] **Commit**

```bash
git add src/infrastructure/iPath.API/Services/Notifications/SseConnectionManager.cs
git commit -m "feat: add 30-second SSE heartbeat to prevent proxy timeouts"
```

---

### Task 8: Last-Event-ID Header Support

**Files:**
- Modify: `src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs`
- Modify: `src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js`

- [ ] **Read Last-Event-ID header as fallback**

`NotificationEndpoints.cs` — after reading query param:
```csharp
var lastEventId = ctx.Request.Query["lastEventId"].FirstOrDefault()
               ?? ctx.Request.Headers["Last-Event-ID"].FirstOrDefault();
```

- [ ] **Remove query param from JS**

`ipath-sse.js` — remove `lastEventId` from the connect function and URL construction. The browser natively sends `Last-Event-ID` header on reconnect:
```javascript
export function connect(dotNetHelper, url) {
    const es = new EventSource(url, { withCredentials: true });
    // ... rest stays the same
}
```

Update callers in `SseClientService.cs` to remove `lastEventId` argument from the `ConnectAsync` method and the JS invocation.

- [ ] **Build to verify**

```bash
dotnet build
```

- [ ] **Commit**

```bash
git add src/infrastructure/iPath.API/Endpoints/NotificationEndpoints.cs src/ui/iPath.RazorLib/wwwroot/js/ipath-sse.js
git commit -m "fix: support Last-Event-ID header for native browser SSE reconnection"
```

---

### Task 9: Build, Run Tests, and Manual Verification

**Files:** None (verification step)

- [ ] **Full build and run tests**

```bash
dotnet build && dotnet test --no-build
```

Expected: Build succeeds, no test failures (skipped tests remain skipped).

- [ ] **Run through manual testing plan**

1. **SSE connects** — Login, check DevTools Network for `events/stream` → 200, `text/event-stream`
2. **Route fix** — Verify no 404 on SSE endpoint → Stream opens successfully
3. **Notification arrives** — Trigger `NodePublished` or `NewAnnotation` → Badge increments, snackbar appears, dropdown shows new item
4. **Domain event silent** — Trigger annotation without subscription → No snackbar, no badge change
5. **Cache invalidation** — Rename a group/community/user → UI updates reflect change
6. **Bell mark-as-read** — Click notification in bell → Navigates to case, badge decrements
7. **Bell mark-all-read** — Click "Mark all as read" → Badge clears, dropdown empty
8. **Notification page** — Navigate to `/notifications` → Full list with paging, correct counts
9. **Delete single** — Click delete icon → Notification removed, count updates
10. **Delete all** — Click "Delete all", confirm dialog → All notifications cleared
11. **Admin review** — Navigate to `/admin/servicerequest/{id}/events` → Events tab shows badges, Notifications tab shows who/what/how
12. **Reconnection** — Briefly disconnect network, reconnect → SSE auto-reconnects, stream resumes

13. **Back link** — Navigate to `/admin/servicerequest/{id}/events`, click back arrow → returns to `/request/{id}`
14. **Group notification settings** — Navigate to `/groups/{id}`, click bell icon → dialog opens showing current notification toggles, save works, badge reflects active settings

- [ ] **Final commit (if any fixes needed)**

```bash
git add -A && git commit -m "chore: final fixes after SSE notification system testing"
```

---

### Task 10: Add Back Link on Admin Events & Notifications Page

**Files:**
- Modify: `src/ui/iPath.RazorLib/Admin/Events/ServiceRequestEventsPage.razor`

- [ ] **Add back button to title area**

In `ServiceRequestEventsPage.razor`, replace the current header `<MudStack Row="true" Spacing="2" Class="mb-4"><MudText Typo="Typo.h5">Events & Notifications</MudText></MudStack>` with one that includes a back button:

```razor
<MudStack Row="true" Spacing="2" Class="mb-4">
    <MudIconButton Icon="@Icons.Material.Filled.ArrowBack"
                   OnClick="@(() => nm.NavigateTo($"/request/{id}"))" />
    <MudText Typo="Typo.h5">Events & Notifications</MudText>
</MudStack>
```

- [ ] **Build to verify**

```bash
dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj
```

- [ ] **Commit**

```bash
git add src/ui/iPath.RazorLib/Admin/Events/ServiceRequestEventsPage.razor
git commit -m "feat: add back link from admin events page to service request detail"
```

---

### Task 11: Add Notification Settings on Group Page

**Files:**
- Create: `src/ui/iPath.RazorLib/Groups/Components/GroupNotificationDialog.razor`
- Create: `src/ui/iPath.RazorLib/Groups/Components/GroupNotificationDialog.razor.cs`
- Modify: `src/ui/iPath.RazorLib/Groups/GroupIndexPage.razor`
- Modify: `src/ui/iPath.RazorLib/_Imports.razor`
- Modify: `src/ui/iPath.RazorLib/Groups/GroupViewModel.cs`

- [ ] **Read existing components for patterns**

Read these files first:
- `src/ui/iPath.RazorLib/Users/Componenets/UserNotificationGrid.razor` and `.razor.cs` — understand the `UserNotificationModel` class and how toggles work
- `src/ui/iPath.RazorLib/Users/Dialogs/NotificationBodySiteFilterDialog.razor` — understand how BodySite filter dialog works
- `src/ui/iPath.RazorLib/Groups/GroupViewModel.cs` — understand existing patterns

- [ ] **Create GroupNotificationDialog.razor**

A compact MudBlazor dialog for editing notification settings for a single group:

```razor
@namespace iPath.Blazor.Componenents.Groups.Components

@inject IPathApi api
@inject ISnackbar snackbar
@inject IStringLocalizer T

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">@T["Notification Settings for {0}", Groupname]</MudText>
    </TitleContent>
    <DialogContent>
        <MudText Typo="Typo.subtitle2" Class="mt-2">@T["Notify as"]</MudText>
        <MudStack Row Spacing="2">
            <MudCheckBox @bind-Value="InApp" Label="@T["In App"]" />
            <MudCheckBox @bind-Value="Email" Label="@T["Email"]" />
        </MudStack>

        <MudText Typo="Typo.subtitle2" Class="mt-4">@T["Notify on"]</MudText>
        <MudCheckBox @bind-Value="NewCase" Label="@T["New case"]" />
        <MudCheckBox @bind-Value="NewAnnotation" Label="@T["Annotation on any case"]" />
        <MudCheckBox @bind-Value="NewAnnotationOnMyCase" Label="@T["Annotation on my case"]" />

        <MudButton OnClick="ShowBodySiteFilter" Class="mt-4"
                   Variant="Variant.Text" Color="Color.Primary"
                   StartIcon="@SettingsIcon">
            @T["Body Site Filter"] @BodySiteFilterString
        </MudButton>
    </DialogContent>
    <FooterContent>
        <MudButton OnClick="Save" Variant="Variant.Filled" Color="Color.Primary"
                   Disabled="@(!HasChange)" StartIcon="@Icons.Material.Filled.Save">
            @T["Save"]
        </MudButton>
        <MudButton OnClick="Cancel" Variant="Variant.Text">@T["Cancel"]</MudButton>
    </FooterContent>
</MudDialog>
```

- [ ] **Add namespace import to _Imports.razor**

`src/ui/iPath.RazorLib/_Imports.razor` — add after the existing Groups import:
```razor
@using iPath.Blazor.Componenents.Groups.Components
```

- [ ] **Create GroupNotificationDialog.razor.cs**

Code-behind that loads/saves the current user's notification settings for this specific group:

```csharp
using iPath.Application.Features.Notifications;
using iPath.Application.Features.Users;
using iPath.Blazor.Componenents.Users;
using iPath.Domain.Entities.Groups;
using MudBlazor;

namespace iPath.Blazor.Componenents.Groups.Components;

public partial class GroupNotificationDialog : ComponentBase
{
    [CascadingParameter] private MudDialogInstance MudDialog { get; set; }
    [Parameter] public Guid GroupId { get; set; }
    [Parameter] public string Groupname { get; set; }
    [Parameter] public Guid UserId { get; set; }

    private bool InApp, Email;
    private bool NewCase, NewAnnotation, NewAnnotationOnMyCase;
    private NotificationSettings Settings = new();
    private bool HasSettings;
    private bool _loaded;
    private bool HasChange => _loaded;

    protected override async Task OnInitializedAsync()
    {
        var resp = await api.GetUserNotification(UserId);
        if (resp.IsSuccessful)
        {
            var dto = resp.Content.FirstOrDefault(n => n.GroupId == GroupId);
            if (dto is not null)
            {
                InApp = dto.Tartget.HasFlag(eNotificationTarget.InApp);
                Email = dto.Tartget.HasFlag(eNotificationTarget.Email);
                NewCase = dto.Source.HasFlag(eNotificationSource.NewCase);
                NewAnnotation = dto.Source.HasFlag(eNotificationSource.NewAnnotation);
                NewAnnotationOnMyCase = dto.Source.HasFlag(eNotificationSource.NewAnnotationOnMyCase);
                Settings = dto.Settings ?? new();
                HasSettings = Settings.BodySiteFilter is not null || Settings.DailyEmailSummary || Settings.UseProfileBodySiteFilter;
            }
        }
        _loaded = true;
    }

    private string SettingsIcon => HasSettings ? Icons.Material.Filled.SettingsSuggest : Icons.Material.Filled.Settings;

    private string BodySiteFilterString
    {
        get
        {
            if (Settings?.BodySiteFilter is not null)
                return Settings.BodySiteFilter.ConceptCodesString;
            return "";
        }
    }

    private async Task ShowBodySiteFilter()
    {
        var model = new UserNotificationModel(
            new UserGroupNotificationDto(UserId, GroupId, eNotificationSource.None, eNotificationTarget.None, Settings, Groupname),
            null);
        var p = new DialogParameters<NotificationBodySiteFilterDialog> { { x => x.Model, model } };
        var o = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dlg = await dialog.ShowAsync<NotificationBodySiteFilterDialog>("Body Site Filter", options: o, parameters: p);
        var r = await dlg.Result;
        if (r is not null && !r.Canceled)
        {
            Settings = model.Settings;
            HasSettings = Settings?.BodySiteFilter is not null || Settings?.DailyEmailSummary == true || Settings?.UseProfileBodySiteFilter == true;
            StateHasChanged();
        }
    }

    private async Task Save()
    {
        var source = eNotificationSource.None;
        if (NewCase) source |= eNotificationSource.NewCase;
        if (NewAnnotation) source |= eNotificationSource.NewAnnotation;
        if (NewAnnotationOnMyCase) source |= eNotificationSource.NewAnnotationOnMyCase;

        var target = eNotificationTarget.None;
        if (InApp) target |= eNotificationTarget.InApp;
        if (Email) target |= eNotificationTarget.Email;

        var dto = new UserGroupNotificationDto(UserId, GroupId, source, target, Settings, Groupname);
        var cmd = new UpdateUserNotificationsCommand(UserId, new[] { dto });
        var resp = await api.UpdateUserNotification(cmd);
        if (resp.IsSuccessful)
        {
            snackbar.AddSuccess(T["Notification settings saved"]);
            MudDialog.Close(DialogResult.Ok(true));
        }
        else
        {
            snackbar.AddError(resp.ErrorMessage);
        }
    }

    private void Cancel() => MudDialog.Cancel();
}
```

- [ ] **Add notification button to GroupIndexPage.razor**

Add `ActionContent` to the `IPathHeader`, showing a bell icon. The icon should reflect whether the current user has active notifications for this group:

```razor
@inject IDialogService dialog
@inject AppState appState

@if (Model is null)
{
    <LoadingMessage />
}
else
{
    <IPathHeader Title="@vm.Model.Name" Typo="Typo.h6">
        <ActionContent>
            @if (appState.IsAuthenticated)
            {
                <MudIconButton Icon="@Icons.Material.Filled.Notifications"
                               Size="Size.Small"
                               OnClick="OpenGroupNotificationDialog" />
            }
        </ActionContent>
    </IPathHeader>

    <ServiceRequestListView GroupId="@vm.Model.Id" ListMode="eRequestFilter.Group" />
}

@code {
    ...

    private async Task OpenGroupNotificationDialog()
    {
        if (Model is null) return;
        var p = new DialogParameters<GroupNotificationDialog>
        {
            { x => x.GroupId, Model.Id },
            { x => x.Groupname, Model.Name },
            { x => x.UserId, appState.User.Id }
        };
        var o = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        await dialog.ShowAsync<GroupNotificationDialog>("Notifications", options: o, parameters: p);
    }
}
```

- [ ] **Build to verify**

```bash
dotnet build src/ui/iPath.RazorLib/iPath.RazorLib.csproj
```

- [ ] **Commit**

```bash
git add src/ui/iPath.RazorLib/Groups/
git add src/ui/iPath.RazorLib/Groups/GroupIndexPage.razor
git commit -m "feat: add notification settings dialog on group page"
```
