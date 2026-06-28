using iPath.Application.Contracts;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Documents;
using iPath.Blazor.Componenents.Documents;
using iPath.Blazor.Componenents.Notifications;
using iPath.Blazor.Componenents.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;

namespace iPath.Blazor.Componenents.ServiceRequests;

public partial class CaseRoomPage
{
    [Inject]
    private AppState AppState { get; set; } = null!;

    [Inject]
    private ILogger<CaseRoomPage> Logger { get; set; } = null!;

    [Inject]
    private SseClientService Sse { get; set; } = null!;

    private Guid RequestId => Guid.Parse(id);
    private IJSObjectReference? _module;
    private DotNetObjectReference<CaseRoomPage>? _dotNetRef;
    private IDisposable? _syncSub;
    private CancellationTokenSource? _pingCts;
    private Guid _sessionId = Guid.NewGuid();
    private bool _isApplyingRemote;
    private bool _initialized;

    internal ViewportState? CurrentViewport { get; set; }
    internal bool SseConnected { get; set; } = true;

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

            var snapshot = await SyncService.JoinAsync(RequestId, _sessionId);
            Participants = snapshot.Participants.ToList();
            StateHasChanged();

            _syncSub = SyncReceiver.Subscribe(RequestId, OnSyncReceived);
            Sse.ConnectionError += OnSseError;

            if (snapshot.Viewport is not null)
                CurrentViewport = snapshot.Viewport;

            var docId = snapshot.ActiveDocumentId ??
                        vm.SelectedRequest?.Documents.FirstOrDefault(n => n.IsSlide)?.Id;

            if (docId.HasValue)
            {
                vm.SelectDocument(docId.Value);
                var doc = vm.SelectedDocument;
                var url = GetTileSourceUrl(doc);
                await _module.InvokeVoidAsync("initOsd", "osd-caseroom", url, _dotNetRef, snapshot.Viewport);
            }
            else
            {
                await _module.InvokeVoidAsync("initOsd", "osd-caseroom", null, _dotNetRef, snapshot.Viewport);
            }

            _pingCts = new CancellationTokenSource();
            _ = StartHeartbeatAsync(_pingCts.Token);
        }
    }

    private async Task StartHeartbeatAsync(CancellationToken ct)
    {
        using var timer = new System.Threading.PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await SyncService.SyncAsync(RequestId, new SyncPayload(null, null, SessionId: _sessionId), ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    [JSInvokable]
    public async Task OnViewportChanged(double x, double y, double zoom)
    {
        if (_isApplyingRemote) return;
        CurrentViewport = new ViewportState(x, y, zoom);
        Logger.LogInformation("Viewport changed: X={X:F4}, Y={Y:F4}, Zoom={Zoom:F4}", x, y, zoom);
        await SyncService.SyncAsync(RequestId, new SyncPayload(null, CurrentViewport, SessionId: _sessionId));
        StateHasChanged();
    }

    private void OnSyncReceived(CaseRoomSyncEvent evt)
    {
        _ = InvokeAsync(async () =>
        {
            if (evt.Payload.SessionId == _sessionId) return;

            _isApplyingRemote = true;

            if (evt.Payload.Action == "Join" || evt.Payload.Action == "Leave")
            {
                if (evt.Payload.Participants is not null)
                {
                    Participants = evt.Payload.Participants.ToList();
                }
            }

            if (evt.Payload.DocumentId.HasValue && _module is not null)
            {
                vm.SelectDocument(evt.Payload.DocumentId.Value);
                var url = GetTileSourceUrl(vm.SelectedDocument);
                await _module.InvokeVoidAsync("openTileSource", url);
            }

            if (evt.Payload.Viewport is not null && _module is not null)
            {
                var vp = evt.Payload.Viewport;
                Logger.LogInformation("Remote viewport received: User={User}, X={X:F4}, Y={Y:F4}, Zoom={Zoom:F4}",
                    evt.DisplayName, vp.X, vp.Y, vp.Zoom);
                CurrentViewport = vp;
                await _module.InvokeVoidAsync("setViewport", vp.X, vp.Y, vp.Zoom);
            }

            _isApplyingRemote = false;
            StateHasChanged();
        });
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
        await SyncService.SyncAsync(RequestId, new SyncPayload(vm.SelectedDocument.Id, null, SessionId: _sessionId));
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
        return doc.IsWSI
            ? $"/files/{doc.Id}.dzi"
            : $"/files/{doc.Id}";
    }

    private void OnSseError(object? sender, EventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            SseConnected = false;
            Logger.LogWarning("SSE connection lost in CaseRoom {RequestId}", RequestId);
            StateHasChanged();
        });
    }

    protected override async ValueTask OnDisposedAsync()
    {
        Sse.ConnectionError -= OnSseError;
        _syncSub?.Dispose();
        _pingCts?.Cancel();
        _pingCts?.Dispose();
        try
        {
            if (_module is not null)
                await _module.InvokeVoidAsync("dispose");
        }
        catch (JSDisconnectedException) { }
        try
        {
            await SyncService.LeaveAsync(RequestId, _sessionId);
        }
        catch { }
        _dotNetRef?.Dispose();
        if (_module is not null)
            await _module.DisposeAsync();
        await base.OnDisposedAsync();
    }
}
