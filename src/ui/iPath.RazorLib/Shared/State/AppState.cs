using iPath.Application.Contracts;
using Microsoft.Extensions.Logging;
using MudBlazor.Interfaces;

namespace iPath.Blazor.Componenents.Shared;

public class AppState(IPathApi api, ILogger<AppState> logger) : IUserSession
{
    public Action OnChange;


    private SessionUserDto _user;

    public SessionUserDto? User => _user;
    public bool IsAuthenticated => _user is not null && _user.Id != Guid.Empty;


    public async Task ReloadSession()
    {
        _user = SessionUserDto.Anonymous;
        try
        {
            var resp = await api.GetSession();
            if (resp.IsSuccessful)
            {
                _user = resp.Content;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "error calling /api/v1/session");
        }
    }

    public void ReloadUser(Guid userId)
    {
        _user = SessionUserDto.Anonymous;
    }

    public Color PresenceColor => Color.Success;



    private ServiceRequestUpdatesDto _stats;
    public async Task<ServiceRequestUpdatesDto> GetNewRequestStats(bool reload)
    {
        if (reload || _stats is null)
        {
            var resp = await api.GetServiceRequestUpdates();
            if (resp.IsSuccessful)
            {
                _stats = resp.Content;
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
            OnChange?.Invoke();
        }
    }

    public int UnreadNotificationCount { get; private set; }
    public bool NotificationDrawerOpen { get; set; }

    public void SetUnreadCount(int count)
    {
        UnreadNotificationCount = count;
        OnChange?.Invoke();
    }

    public void IncrementUnreadCount()
    {
        UnreadNotificationCount++;
        OnChange?.Invoke();
    }

    public void DecrementUnreadCount()
    {
        if (UnreadNotificationCount > 0) UnreadNotificationCount--;
        OnChange?.Invoke();
    }

    public void ToggleNotificationDrawer()
    {
        NotificationDrawerOpen = !NotificationDrawerOpen;
        OnChange?.Invoke();
    }

    public void CloseNotificationDrawer()
    {
        NotificationDrawerOpen = false;
        OnChange?.Invoke();
    }
}
