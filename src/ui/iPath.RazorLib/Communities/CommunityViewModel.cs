using iPath.Blazor.Componenents.Admin.Groups;
using iPath.Blazor.ServiceLib.ApiClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.Componenents.Communities;

public class CommunityViewModel(IPathApi api,
    ISnackbar snackbar,
    IDialogService dialog,
    IMemoryCache cache,
    IStringLocalizer T,
    ILogger<GroupAdminViewModel> logger)
    : IViewModel
{
    const string communityListCacheKey = "admin.communitylist";
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);

    public async Task<IEnumerable<CommunityListDto>> GetAllAsync()
    {
        try
        {
            await _cacheLock.WaitAsync();
            if (!cache.TryGetValue<IEnumerable<CommunityListDto>>(communityListCacheKey, out var _communityList))
            {
                var query = new GetCommunityListQuery { PageSize = null };
                var resp = await api.GetCommunityList(query);
                if (resp.IsSuccessful)
                {
                    _communityList = resp.Content.Items.OrderBy(c => c.Name);

                    var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(60));
                    cache.Set(communityListCacheKey, _communityList, opts);
                }
            }
            return _communityList;
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

    private void DeleteCommunityListCache()
    {
        cache.Remove(communityListCacheKey);
    }

}
