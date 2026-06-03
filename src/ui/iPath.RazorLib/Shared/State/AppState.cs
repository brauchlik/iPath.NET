using iPath.Application.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace iPath.Blazor.Componenents.Shared;

public class AppState : IUserSession, IDisposable
{
    private readonly IPathApi _api;
    private readonly AuthenticationStateProvider _authProvider;
    private readonly ILogger<AppState> _logger;
    private SessionUserDto _user = SessionUserDto.Anonymous;

    public AppState(IPathApi api, AuthenticationStateProvider authProvider, ILogger<AppState> logger)
    {
        _api = api;
        _authProvider = authProvider;
        _logger = logger;
        authProvider.AuthenticationStateChanged += OnAuthStateChanged;
    }

    public event Action? OnChange;

    public SessionUserDto? User => _user;
    public bool IsAuthenticated => _user is not null && _user.Id != Guid.Empty;

    public void NotifyStateChanged() => OnChange?.Invoke();

    /// <summary>
    /// Called once from MainLayout.OnInitializedAsync after attaching OnChange subscriber.
    /// </summary>
    public async Task LoadSessionAsync() => await LoadCoreAsync();

    /// <summary>
    /// Explicit refresh for SSE system events etc.
    /// </summary>
    public async Task RefreshAsync() => await LoadCoreAsync();

    private async Task LoadCoreAsync()
    {
        var previous = _user;
        _user = SessionUserDto.Anonymous;

        try
        {
            var resp = await _api.GetSession();
            if (resp.IsSuccessful && resp.Content is not null && resp.Content.Id != Guid.Empty)
                _user = resp.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading session");
        }

        if (previous.Id != _user.Id)
            OnChange?.Invoke();
    }

    private async void OnAuthStateChanged(Task<AuthenticationState> stateTask)
    {
        try
        {
            await stateTask;
            await LoadCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling auth state change");
        }
    }

    public void Dispose()
    {
        _authProvider.AuthenticationStateChanged -= OnAuthStateChanged;
    }

    public void ReloadUser(Guid userId)
    {
        _user = SessionUserDto.Anonymous;
        OnChange?.Invoke();
    }

    public Color PresenceColor => Color.Success;

    private ServiceRequestUpdatesDto _stats;
    public async Task<ServiceRequestUpdatesDto> GetNewRequestStats(bool reload)
    {
        if (!IsAuthenticated) return _stats;
        if (reload || _stats is null)
        {
            var resp = await _api.GetServiceRequestUpdates();
            if (resp.IsSuccessful)
            {
                _stats = resp.Content;
            }
            else
            {
                _stats = new ServiceRequestUpdatesDto();
            }
        }
        return _stats;
    }
    public bool StatsLoaded => _stats is not null;

    public void ServiceRequestVisited(Guid id)
    {
        if (_stats is not null)
        {
            _stats.NewRequests.RemoveAll(x => x.Id == id);
            _stats.NewAnnotations.RemoveAll(x => x.Id == id);
            NotifyStateChanged();
        }
    }

    public int UnreadNotificationCount { get; private set; }

    public void SetUnreadCount(int count)
    {
        UnreadNotificationCount = count;
        NotifyStateChanged();
    }

    public void IncrementUnreadCount()
    {
        UnreadNotificationCount++;
        NotifyStateChanged();
    }

    public void DecrementUnreadCount()
    {
        if (UnreadNotificationCount > 0) UnreadNotificationCount--;
        NotifyStateChanged();
    }
}
