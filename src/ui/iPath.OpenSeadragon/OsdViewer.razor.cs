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
            _dotNetRef = DotNetObjectReference.Create(this);
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/iPath.OpenSeadragon/js/ipath-viewer.js");

            _lastImagePath = ImagePath;
            _initialized = true;
            await _module.InvokeVoidAsync("initOsd", _elementId, ImagePath, _dotNetRef, null);
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
        var style = "background-color: black; position: relative;";
        if (MaxWidth.HasValue)
            style += $" max-width: {MaxWidth}px;";
        _paperStyle = style;
    }

    private async Task LoadTileSourceAsync(string url)
    {
        if (_module is null) return;
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();
        await _module.InvokeVoidAsync("openTileSource", url);
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
                await _module.InvokeVoidAsync("dispose");
        }
        catch (JSDisconnectedException) { }
        _dotNetRef?.Dispose();
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
