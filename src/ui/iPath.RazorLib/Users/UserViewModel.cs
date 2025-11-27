using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.Componenents.Users;

public class UserViewModel(IPathApi api, 
    ISnackbar snackbar, 
    IDialogService srvDialog,
    IMemoryCache cache,
    IStringLocalizer T,
    ILogger<UserViewModel> logger) : IViewModel
{
    public async Task ShowProfileAsync(Guid UserId) => ShowProfileAsync(await GetProfileAsync(UserId));

    public async Task ShowProfileAsync(UserProfile? profile)
    {
        if (profile == null || srvDialog == null) return;

        var parameters = new DialogParameters<UserProfileDialog> { { x => x.Profile, profile } };
        var options = new DialogOptions { CloseOnEscapeKey = true };
        var dialog = await srvDialog.ShowAsync<UserProfileDialog>("User Profile", parameters: parameters, options: options);
        var result = await dialog.Result;
    }


    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);

    public async Task<UserProfile> GetProfileAsync(Guid userid)
    {
        var cacheKey = $"User_{userid}";

        try
        {
            await _cacheLock.WaitAsync();
            if (!cache.TryGetValue(cacheKey, out UserProfile profile))
            {
                var resp = await api.GetUser(userid);
                if (resp.IsSuccessful)
                {
                    profile = resp.Content.Profile;
                }
                else
                {
                    logger.LogWarning("Could not find User {0}", userid);
                    profile = new()
                    {
                        UserId = userid,
                        Username = "not found"
                    };
                }
                var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(5));
                cache.Set(cacheKey, profile, opts);
            }
            return profile;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
        finally
        {
            _cacheLock.Release();
        }

        return null;
    }

    public async Task<UserDto?> GetUserAsync(Guid id)
    {
        var res = await api.GetUser(id);
        if (res.IsSuccessful) return res.Content;
        snackbar.AddWarning(res.ErrorMessage);
        return null;
    }

    public async Task<SessionUserDto> GetCurrentSessionAsync()
    {
        var res = await api.GetSession();
        if (res.IsSuccessful) return res.Content;
        return SessionUserDto.Anonymous;
    }

    public async Task UpdateProfile(Guid userId, UserProfile profile, bool showSuccess)
    {
        var resp = await api.UpdateProfile(new UpdateUserProfileCommand(userId, profile));
        if (!resp.IsSuccessful)
        {
            snackbar.AddWarning(resp.ErrorMessage);
        }
        else if (showSuccess) 
        { 
            snackbar.Add(T["Profile has been saved"], Severity.Success);
        }
    }

    public async Task<IEnumerable<UserListDto>> Search(string? search, CancellationToken ct)
    {
        if (search is not null)
        {
            var query = new GetUserListQuery { SearchString = search, Page = 0, PageSize = 100 };
            var resp = await api.GetUserList(query);
            if (resp.IsSuccessful)
            {
                return resp.Content.Items;
            }
        }
        return new List<UserListDto>();
    }
    public async Task<IEnumerable<OwnerDto>> SearchOwners(string? search, CancellationToken ct)
    {
        if (search is not null)
        {
            var query = new GetUserListQuery { SearchString = search, Page = 0, PageSize = 100 };
            var resp = await api.GetUserList(query);
            if (resp.IsSuccessful)
            {
                return resp.Content.Items.Select(u => new OwnerDto(Id: u.Id, Username: u.Username, Email: u.Email));
            }
        }
        return new List<OwnerDto>();
    }
}
