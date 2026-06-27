using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Text.Json;

namespace iPath.Blazor.Componenents.Notifications;

public class SseClientService : IAsyncDisposable
{
    private readonly ILogger<SseClientService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly bool _isServerMode;
    private readonly List<IDisposable> _subscriptions = new();

    // Server mode: in-process event bus subscription
    private INotificationEventBus? _eventBus;

    // WASM mode: JS interop fields
    private IJSRuntime? _js;
    private IJSObjectReference? _module;
    private IJSObjectReference? _eventSource;
    private DotNetObjectReference<SseClientService>? _dotNetHelper;
    private string? _lastEventId;

    public event EventHandler<NotificationDto>? NotificationReceived;
    public event EventHandler<DomainEventSummary>? DomainEventReceived;
    public event EventHandler<SystemEventHint>? SystemEventReceived;
    public event EventHandler<CaseRoomSyncEvent>? CaseRoomSyncReceived;
    public event EventHandler? ConnectionError;

    public SseClientService(IJSRuntime js, IServiceProvider serviceProvider, ILogger<SseClientService> logger)
    {
        _js = js;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _isServerMode = !OperatingSystem.IsBrowser();
    }

    public async Task ConnectAsync(string url, Guid userId)
    {
        if (_isServerMode)
        {
            ConnectServerMode(userId);
            return;
        }

        // WASM mode: use browser EventSource
        try
        {
            _module = await _js!.InvokeAsync<IJSObjectReference>("import", "./_content/iPath.Blazor.Componenents/js/ipath-sse.js");
            _dotNetHelper = DotNetObjectReference.Create(this);
            _eventSource = await _module.InvokeAsync<IJSObjectReference>("connect", _dotNetHelper, url);
            _logger.LogInformation("SSE connected to {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect SSE");
        }
    }

    private void ConnectServerMode(Guid userId)
    {
        try
        {
            _eventBus = _serviceProvider.GetRequiredService<INotificationEventBus>();
            _subscriptions.Add(_eventBus.SubscribeNotifications(userId, OnEventBusNotification));
            _subscriptions.Add(_eventBus.SubscribeDomainEvents(OnEventBusDomainEvent));
            _subscriptions.Add(_eventBus.SubscribeSystemEvents(OnEventBusSystemEvent));
            _subscriptions.Add(_eventBus.SubscribeCaseRoomSync(OnEventBusCaseRoomSync));
            _logger.LogInformation("SSE connected in Server mode for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to NotificationEventBus");
        }
    }

    private void OnEventBusNotification(NotificationDto dto)
    {
        NotificationReceived?.Invoke(this, dto);
    }

    private void OnEventBusDomainEvent(DomainEventSummary evt)
    {
        DomainEventReceived?.Invoke(this, evt);
    }

    private void OnEventBusSystemEvent(SystemEventHint hint)
    {
        SystemEventReceived?.Invoke(this, hint);
    }

    private void OnEventBusCaseRoomSync(CaseRoomSyncEvent evt)
    {
        CaseRoomSyncReceived?.Invoke(this, evt);
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
    public void OnCaseRoomSync(string data, string lastEventId)
    {
        _lastEventId = lastEventId;
        try
        {
            var evt = JsonSerializer.Deserialize<CaseRoomSyncEvent>(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (evt is not null)
                CaseRoomSyncReceived?.Invoke(this, evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize caseroom-sync");
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
        if (_isServerMode)
        {
            foreach (var sub in _subscriptions)
                sub.Dispose();
            _subscriptions.Clear();
            return;
        }

        // WASM mode: dispose JS interop resources
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
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // expected during circuit disconnect or page navigation
            }
        }
    }
}
