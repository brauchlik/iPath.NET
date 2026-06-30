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

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Parameter]
    [SupplyParameterFromQuery]
    public string? token { get; set; }

    private Guid RequestId => Guid.Parse(id);
    private IJSObjectReference? _module;
    private DotNetObjectReference<CaseRoomPage>? _dotNetRef;
    private IDisposable? _syncSub;
    private CancellationTokenSource? _pingCts;
    private Guid _sessionId = Guid.NewGuid();
    private bool _isApplyingRemote;
    private bool _initialized;
    private bool _isGuest;
    private bool _isController;
    private Guid? _controllerSessionId;
    private bool _pointerToolActive;
    private System.Threading.CancellationTokenSource? _pointerHideCts;

    private Guid? _activeDocumentId;
    private bool? _activeDocumentIsWSI;
    internal string? _activeFilename;

    internal ViewportState? CurrentViewport { get; set; }
    internal bool SseConnected { get; set; } = true;

    private List<Participant> Participants { get; set; } = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_initialized)
        {
            _initialized = true;
            _isGuest = !AppState.IsAuthenticated;

            if (!_isGuest)
            {
                await vm.LoadNode(RequestId, loadGroup: false);
            }

            _module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/iPath.Blazor.Componenents/js/ipath-caseroom.js");

            _dotNetRef = DotNetObjectReference.Create(this);

            CaseRoomSnapshot? snapshot = null;
            try
            {
                Guid? initDocId = null;
                bool? initIsWSI = null;
                string? initFilename = null;
                
                if (!_isGuest)
                {
                    initDocId = vm.SelectedDocument?.Id ?? vm.SelectedRequest?.Documents.FirstOrDefault(n => n.IsSlide)?.Id;
                    if (initDocId.HasValue)
                    {
                        var doc = vm.SelectedRequest?.Documents.FirstOrDefault(d => d.Id == initDocId.Value);
                        initIsWSI = doc?.IsWSI;
                        initFilename = doc?.File?.Filename;
                    }
                }
                snapshot = await SyncService.JoinAsync(RequestId, _sessionId, token, initDocId, initIsWSI, initFilename);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to join CaseRoom");
                Snackbar.Add("Could not join presentation. Redirecting to login...", Severity.Error);
                NavigationManager.NavigateTo($"/Account/Login?returnUrl={Uri.EscapeDataString(NavigationManager.Uri)}", forceLoad: true);
                return;
            }

            if (snapshot is null)
            {
                Logger.LogWarning("JoinCaseRoom returned null snapshot");
                Snackbar.Add("Could not join presentation.", Severity.Error);
                NavigationManager.NavigateTo($"/Account/Login?returnUrl={Uri.EscapeDataString(NavigationManager.Uri)}", forceLoad: true);
                return;
            }

            Participants = snapshot.Participants.ToList();
            _controllerSessionId = snapshot.ControllingSessionId;
            _isController = _controllerSessionId == _sessionId;
            StateHasChanged();

            _syncSub = SyncReceiver.Subscribe(RequestId, OnSyncReceived);

            Sse.ConnectionError += OnSseError;

            if (snapshot.Viewport is not null)
                CurrentViewport = snapshot.Viewport;

            _activeDocumentId = snapshot.ActiveDocumentId;
            if (_activeDocumentId == Guid.Empty)
                _activeDocumentId = vm.SelectedRequest?.Documents.FirstOrDefault(n => n.IsSlide)?.Id;
            _activeDocumentIsWSI = snapshot.ActiveDocumentIsWSI;
            _activeFilename = snapshot.ActiveDocumentFilename;

            if (_activeDocumentId.HasValue)
            {
                if (!_isGuest)
                {
                    vm.SelectDocument(_activeDocumentId.Value);
                    _activeDocumentIsWSI = vm.SelectedDocument?.IsWSI ?? _activeDocumentIsWSI;
                    _activeFilename = vm.SelectedDocument?.File?.Filename ?? _activeFilename;
                    
                    if (snapshot.ActiveDocumentId == Guid.Empty && vm.SelectedDocument is not null)
                    {
                        _ = SyncService.SyncAsync(RequestId, new SyncPayload(
                            vm.SelectedDocument.Id, null, SessionId: _sessionId, 
                            IsWSI: vm.SelectedDocument.IsWSI, Filename: vm.SelectedDocument.File?.Filename));
                    }
                }
                var url = GetTileSourceUrl(_activeDocumentId, _activeDocumentIsWSI);
                await _module.InvokeVoidAsync("initOsd", "osd-caseroom", url, _dotNetRef, snapshot.Viewport);
            }
            else
            {
                await _module.InvokeVoidAsync("initOsd", "osd-caseroom", null, _dotNetRef, snapshot.Viewport);
            }

            if (_module is not null)
                await _module.InvokeVoidAsync("setMouseNavEnabled", _isController);

            if (snapshot.Pointer is not null && snapshot.Pointer.IsVisible && _module is not null)
            {
                if (_isController)
                {
                    _pointerToolActive = true;
                    await _module.InvokeVoidAsync("setPointerToolActive", true);
                    await _module.InvokeVoidAsync("showPointer", snapshot.Pointer.X, snapshot.Pointer.Y, true);
                }
                else
                {
                    await _module.InvokeVoidAsync("showPointer", snapshot.Pointer.X, snapshot.Pointer.Y, false);
                }
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
            if (evt.Payload.Action != "ControllerChanged" && evt.Payload.SessionId == _sessionId) return;

            if (evt.Payload.Action == "HostLeft" && _isGuest)
            {
                Logger.LogInformation("Host left the session. Redirecting guest...");
                Snackbar.Add("The presenter has left the session. Closing CaseRoom.", Severity.Warning);
                NavigationManager.NavigateTo($"/Account/Login?returnUrl={Uri.EscapeDataString(NavigationManager.Uri)}", forceLoad: true);
                return;
            }

            _isApplyingRemote = true;

            if (evt.Payload.Action == "Join" || evt.Payload.Action == "Leave" || evt.Payload.Action == "HostLeft" || evt.Payload.Action == "ControllerChanged")
            {
                if (evt.Payload.Participants is not null)
                {
                    Participants = evt.Payload.Participants.ToList();
                }

                if (evt.Payload.ControllingSessionId != _controllerSessionId || evt.Payload.Action == "ControllerChanged")
                {
                    _controllerSessionId = evt.Payload.ControllingSessionId;
                    var wasController = _isController;
                    _isController = _controllerSessionId == _sessionId;

                    if (_isController)
                    {
                        if (evt.Payload.Pointer is not null && evt.Payload.Pointer.IsVisible)
                        {
                            _pointerToolActive = true;
                            if (_module is not null)
                            {
                                await _module.InvokeVoidAsync("setPointerToolActive", true);
                                await _module.InvokeVoidAsync("showPointer", evt.Payload.Pointer.X, evt.Payload.Pointer.Y, true);
                            }
                        }
                    }
                    else if (wasController && _pointerToolActive)
                    {
                        _pointerToolActive = false;
                        if (_module is not null)
                        {
                            await _module.InvokeVoidAsync("setPointerToolActive", false);
                        }
                    }

                    if (_module is not null)
                        await _module.InvokeVoidAsync("setMouseNavEnabled", _isController);
                    Logger.LogInformation("Control state changed: IsController={IsController}, ControllerSessionId={ControllerId}",
                        _isController, _controllerSessionId);
                }
            }

            if (evt.Payload.DocumentId.HasValue && _module is not null)
            {
                _activeDocumentId = evt.Payload.DocumentId.Value;
                _activeDocumentIsWSI = evt.Payload.IsWSI;
                _activeFilename = evt.Payload.Filename;
                
                if (!_isGuest)
                {
                    vm.SelectDocument(_activeDocumentId.Value);
                    _activeDocumentIsWSI = vm.SelectedDocument?.IsWSI ?? _activeDocumentIsWSI;
                    _activeFilename = vm.SelectedDocument?.File?.Filename ?? _activeFilename;
                }
                
                var url = GetTileSourceUrl(_activeDocumentId, _activeDocumentIsWSI);
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

            if (evt.Payload.Pointer is not null && _module is not null)
            {
                var ptr = evt.Payload.Pointer;
                if (ptr.IsVisible)
                {
                    await _module.InvokeVoidAsync("showPointer", ptr.X, ptr.Y, _isController);
                }
                else
                {
                    await _module.InvokeVoidAsync("hidePointer");
                }
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

    private async Task ShareRoom()
    {
        try
        {
            var tokenString = await SyncService.CreateShareTokenAsync(RequestId, _activeDocumentId, _activeDocumentIsWSI, _activeFilename);
            var shareUrl = NavigationManager.ToAbsoluteUri($"/request/{id}/caseroom?token={tokenString}").ToString();
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", shareUrl);
            Snackbar.Add("Share link copied to clipboard!", Severity.Success);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create share link");
            Snackbar.Add("Failed to create share link.", Severity.Error);
        }
    }

    private async Task BroadcastDocumentChange()
    {
        if (vm.SelectedDocument is null) return;
        _activeDocumentId = vm.SelectedDocument.Id;
        _activeDocumentIsWSI = vm.SelectedDocument.IsWSI;
        _activeFilename = vm.SelectedDocument.File?.Filename;
        
        await SyncService.SyncAsync(RequestId, new SyncPayload(vm.SelectedDocument.Id, null, SessionId: _sessionId, IsWSI: vm.SelectedDocument.IsWSI, Filename: vm.SelectedDocument.File?.Filename));
        if (_module is not null)
            await _module.InvokeVoidAsync("openTileSource", GetTileSourceUrl(_activeDocumentId, _activeDocumentIsWSI));
    }

    private async Task TakeControl()
    {
        await SyncService.SyncAsync(RequestId, new SyncPayload(null, null, _sessionId, "TakeControl"));
    }

    private async Task ReleaseControl()
    {
        if (_pointerToolActive)
        {
            await OnPointerToggleChanged(false);
        }
        await SyncService.SyncAsync(RequestId, new SyncPayload(null, null, _sessionId, "ReleaseControl"));
    }

    private async Task OnPointerToggleChanged(bool active)
    {
        _pointerToolActive = active;
        if (_module is not null)
        {
            if (active)
            {
                // Retrieve current viewport center from JS
                var center = await _module.InvokeAsync<ViewportState>("getViewportCenter");
                
                // Show pointer locally (draggable)
                await _module.InvokeVoidAsync("showPointer", center.X, center.Y, true);
                
                // Broadcast initial pointer placement
                await SyncService.SyncAsync(RequestId, new SyncPayload(
                    DocumentId: null,
                    Viewport: null,
                    SessionId: _sessionId,
                    Pointer: new PointerState(center.X, center.Y, IsVisible: true)
                ));
            }
            else
            {
                await _module.InvokeVoidAsync("hidePointer");
                await SyncService.SyncAsync(RequestId, new SyncPayload(
                    DocumentId: null,
                    Viewport: null,
                    SessionId: _sessionId,
                    Pointer: new PointerState(0, 0, IsVisible: false)
                ));
            }
        }
    }

    [JSInvokable]
    public async Task OnPointerMoved(double x, double y, bool isVisible)
    {
        if (!_isController) return;

        // Broadcast the pointer placement to all participants
        await SyncService.SyncAsync(RequestId, new SyncPayload(
            DocumentId: null,
            Viewport: null,
            SessionId: _sessionId,
            Pointer: new PointerState(x, y, isVisible)
        ));
    }

    [JSInvokable]
    public async Task OnPointerHidden()
    {
        if (!_isController) return;
        _pointerToolActive = false;
        await SyncService.SyncAsync(RequestId, new SyncPayload(
            DocumentId: null,
            Viewport: null,
            SessionId: _sessionId,
            Pointer: new PointerState(0, 0, IsVisible: false)
        ));
        StateHasChanged();
    }

    private async Task ExitRoom()
    {
        vm.SelectDocument(null);
        if (_isGuest)
        {
            try
            {
                await JS.InvokeVoidAsync("eval", "document.cookie = 'CaseRoomGuestToken=; Path=/; Expires=Thu, 01 Jan 1970 00:00:01 GMT;'");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to clear guest cookie");
            }
            NavigationManager.NavigateTo("/Account/Login", forceLoad: true);
        }
        else
        {
            await vm.GoUpRequestPage();
        }
    }

    private string GetTileSourceUrl(Guid? docId, bool? isWSI)
    {
        if (docId is null) return string.Empty;
        var baseUrl = isWSI == true
            ? $"/api/v1/documents/files/{docId}.dzi"
            : $"/api/v1/documents/files/{docId}";

        return string.IsNullOrEmpty(token) ? baseUrl : $"{baseUrl}?token={token}";
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
        _pointerHideCts?.Cancel();
        _pointerHideCts?.Dispose();
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
