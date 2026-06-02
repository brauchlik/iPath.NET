using iPath.Application.Contracts;
using iPath.Application.Features.Users;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor.Interfaces;
using System.Security.Claims;

namespace iPath.Blazor.Componenents.Shared;

public class AppState(IPathApi api, AuthenticationStateProvider authStateProvider, ILogger<AppState> logger) : IUserSession
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
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            var principal = authState.User;

            if (principal.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdClaim, out var userId) && userId != Guid.Empty)
                {
                    var resp = await api.GetUser(userId);
                    if (resp.IsSuccessful && resp.Content is not null)
                    {
                        _user = ToSessionUser(resp.Content);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reloading session");
        }
    }

    private static SessionUserDto ToSessionUser(UserDto user) => new(
        Id: user.Id,
        Username: user.Username,
        Email: user.Email,
        Initials: user.Profile?.Initials ?? "",
        roles: user.Roles?.Select(r => r.Name).ToArray() ?? [],
        communities: user.CommunityMembership?.ToDictionary(c => c.CommunityId, c => c.Role) ?? null,
        groups: user.GroupMembership?.ToList() ?? null
    );

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
}
