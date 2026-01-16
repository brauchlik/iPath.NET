using iPath.Blazor.Componenents.Admin.Groups;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.Componenents.Communities;

public class CommunityViewModel(IPathApi api,
    ISnackbar snackbar,
    IDialogService dialog,
    IMemoryCache cache,
    IStringLocalizer T,
    NavigationManager nm,
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



    public CommunityDto? SelectedCommunity { get; private set; }

    public async Task<CommunityDto?> LoadCommunity(Guid? id)
    {
        SelectedCommunity = null;
        if (id.HasValue)
        {
            var resp = await api.GetCommunity(id.Value);
            if (resp.IsSuccessful)
            {
                SelectedCommunity = resp.Content;
            }
            else
            {
                snackbar.AddError(resp.ErrorMessage);
            }
        }
        return SelectedCommunity;
    }




    public async Task<TableData<GroupListDto>> GetGroupTableAsync(TableState state, CancellationToken ct)
    {
        if (SelectedCommunity is not null)
        {
            var query = state.BuildQuery(new GetGroupListQuery { CommunityId = SelectedCommunity.Id, IncludeCounts = true });
            var resp = await api.GetGroupList(query);
            if (resp.IsSuccessful)
            {
                return resp.Content.ToTableData();
            }

            snackbar.AddError(resp.ErrorMessage);
        }
        return new TableData<GroupListDto>();
    }

    public void GotoGroup(Guid groupId)
    {
        if (groupId != Guid.Empty)
        {
            nm.NavigateTo($"groups/{groupId}");
        }
    }
}
