using iPath.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.Componenents.Users;

public class UserViewModel(IPathApi api, ISnackbar snackbar, IDialogService srvDialog, IMemoryCache cache, ILogger<UserViewModel> logger) : IViewModel
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

    public async Task<UserProfile> GetProfileAsync(Guid userid)
    {
        var cacheKey = $"User_{userid}";

        try
        {
            if (!cache.TryGetValue(cacheKey, out UserProfile profile))
            {
                var resp = await api.GetUser(userid);
                if (resp.IsSuccessful)
                {
                    profile = resp.Content.Profile;
                    var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(5));
                    cache.Set(cacheKey, profile, opts);
                }
            }
            return profile;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
        return null;
    }
}
