using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace iPath.OpenSeadragon;

public partial class OsdViewer : IAsyncDisposable
{
    [Parameter, EditorRequired] public string ImagePath { get; set; } = string.Empty;
    [Parameter] public int? MaxWidth { get; set; }
    [Parameter] public int? MaxHeight { get; set; } = 600;

    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    private IJSObjectReference? _module;
    private DotNetObjectReference<OsdViewer>? _dotNetRef;
    private readonly string _elementId = $"osd-viewer-{Guid.NewGuid():N}";
    private string _viewerWidth = "100%";
    private string _viewerHeight = "600px";
    private string _paperStyle = "background-color: black; position: relative;";
    private bool _isLoading = true;
    private string? _errorMessage;
    private string? _lastImagePath;
    private bool _initialized;

    protected override void OnInitialized()
    {
        ApplySize();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/iPath.OpenSeadragon/js/ipath-viewer.js");

                _lastImagePath = ImagePath;
                _initialized = true;
                await _module.InvokeVoidAsync("initOsd", _elementId, ImagePath, _dotNetRef, null);
            }
            catch (Exception ex)
            {
                // Surface JS init failures as the existing MudAlert instead of a silent
                // black box. JSDisconnectedException covers the reload race; everything
                // else (CDN blocked, module MIME error, OSD init failure) ends up here.
                _errorMessage = $"Viewer init failed: {ex.Message}";
                _isLoading = false;
                StateHasChanged();
            }
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized) return;
        ApplySize();
        if (ImagePath != _lastImagePath)
        {
            _lastImagePath = ImagePath;
            await LoadTileSourceAsync(ImagePath);
        }
    }

    private void ApplySize()
    {
        _viewerWidth = MaxWidth.HasValue ? $"{MaxWidth}px" : "100%";
        _viewerHeight = MaxHeight.HasValue ? $"{MaxHeight}px" : "600px";
        // Set explicit width/height on the MudPaper itself, not just the inner div. The MudPaper
        // was relying on its parent flex container for sizing - when that container collapsed
        // (e.g. on the slideshow page) the MudPaper collapsed to 0x0, the OSD canvas had no
        // room to paint, and clicks fell through to the surrounding slideshow MudPaper
        // (@onclick="GotoNext" - which is why clicking still advanced slides).
        _paperStyle = $"width: {_viewerWidth}; height: {_viewerHeight}; background-color: black; position: relative;";
    }

    private async Task LoadTileSourceAsync(string url)
    {
        if (_module is null) return;
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();
        await _module.InvokeVoidAsync("openTileSource", _elementId, url);
    }

    [JSInvokable]
    public Task OnOsdOpened()
    {
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnOsdLoading()
    {
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnOsdError(string message)
    {
        _isLoading = false;
        _errorMessage = message;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
                await _module.InvokeVoidAsync("dispose", _elementId);
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }

        _dotNetRef?.Dispose();

        // Same circuit-disconnect race as above: on a page reload the Blazor circuit is disposed
        // before this component's DisposeAsync runs, and the JS module's DisposeAsync() throws
        // JSDisconnectedException as a result. Swallow it - the browser is gone, nothing to
        // clean up. ObjectDisposedException covers double-disposal if Dispose is somehow called twice.
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
        }
    }
}
