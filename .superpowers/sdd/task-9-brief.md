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

