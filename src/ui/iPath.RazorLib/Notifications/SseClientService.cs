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

    public event EventHandler<NotificationDto>? NotificationReceived;
    public event EventHandler<DomainEventSummary>? DomainEventReceived;
    public event EventHandler<SystemEventHint>? SystemEventReceived;
    public event EventHandler? ConnectionError;

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
                NotificationReceived?.Invoke(this, dto);
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
                DomainEventReceived?.Invoke(this, evt);
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
                SystemEventReceived?.Invoke(this, hint);
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
        ConnectionError?.Invoke(this, EventArgs.Empty);
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
