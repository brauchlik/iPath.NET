using iPath.Application.Contracts;
using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.ServiceLib.Services;

public class QuestionnaireCacheClient(IMemoryCache cache, IPathApi api, ILogger<QuestionnaireCacheClient> logger) 
{
    public async Task<QuestionnaireEntity?> GetQuestionnaireAsync(string Id, int? Version = null)
    {
        if (string.IsNullOrEmpty(Id)) return null;

        var cachekey = $"qr_{Id}" + (Version.HasValue ? $"_{Version}" : "");

        if (!cache.TryGetValue(cachekey, out QuestionnaireEntity? q))
        {
            logger.LogInformation("loading questionnaire {0}", Id);

            var resp = await api.GetQuestionnaire(Id, Version);
            if (resp.IsSuccessful)
            {
                q = resp.Content;

                var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(5));
                cache.Set(cachekey, q, opts);
            }
            else
            {
                logger.LogWarning("loading questionnaire {0}/{1} failed: {2}, {3}", Id, Version, resp.StatusCode, resp.Error.Message);
            }
        }
        return q;
    }

    public async Task<string?> GetQuestionnaireResourceAsync(string Id, int? Version = null)
        => (await GetQuestionnaireAsync(Id, Version))?.Resource;

    public async Task<string?> GetQuestionnaireNameAsync(string Id, int? Version = null)
        => (await GetQuestionnaireAsync(Id, Version))?.Name;

    void ClearCache(string Id, int? Version = null)
    {
        var cachekey = $"qr_{Id}" + (Version.HasValue ? $"_{Version}" : "");
        cache.Remove(cachekey);
    }
}
