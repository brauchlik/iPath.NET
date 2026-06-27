using iPath.Application.Contracts;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Documents;
using iPath.Blazor.Componenents.Documents;
using iPath.Blazor.Componenents.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor.Services;

namespace iPath.Blazor.Componenents.ServiceRequests;

public partial class CaseRoomPage
{
    [Inject]
    private AppState AppState { get; set; } = null!;

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
                "import", "./_content/iPath.Blazor.Componenents/js/ipath-caseroom.js");

            _dotNetRef = DotNetObjectReference.Create(this);

            var snapshot = await SyncService.JoinAsync(RequestId);
            Participants = snapshot.Participants.ToList();
            StateHasChanged();

            _syncSub = SyncReceiver.Subscribe(RequestId, e => _ = OnSyncReceived(e));

            var docId = snapshot.ActiveDocumentId ??
                        vm.SelectedRequest?.Documents.FirstOrDefault(n => n.IsSlide)?.Id;

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
        if (evt.UserId == AppState.User?.Id) return;

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

    private async Task OnSwipeHandler(SwipeEventArgs args)
    {
        if (args.SwipeDirection == SwipeDirection.RightToLeft)
            await GotoNext();
        else if (args.SwipeDirection == SwipeDirection.LeftToRight)
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

    private string GetTileSourceUrl(DocumentDto? doc)
    {
        if (doc is null) return string.Empty;
        return doc.FileExtension.ToLower() == ".vsi"
            ? $"/files/{doc.Id}.dzi"
            : $"/files/{doc.Id}";
    }

    protected override async ValueTask OnDisposedAsync()
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
        catch { }
        _dotNetRef?.Dispose();
        if (_module is not null)
            await _module.DisposeAsync();
        await base.OnDisposedAsync();
    }
}
