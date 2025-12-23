using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.Componenents.Groups;

public class GroupCache(IMemoryCache cache, IPathApi api, ILogger<GroupCache> logger)
{
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);
    public async Task<GroupDto?> GetGroupAsync(Guid Id)
    {
        try
        {
            await _cacheLock.WaitAsync();
            var key = $"group_{Id}";
            if (!cache.TryGetValue<GroupDto>(key, out var group))
            {
                var resp = await api.GetGroup(Id);
                if (resp.IsSuccessful)
                {
                    group = resp.Content;
                    var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                    cache.Set(key, group, opts);
                }
            }
            return group;
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
}
