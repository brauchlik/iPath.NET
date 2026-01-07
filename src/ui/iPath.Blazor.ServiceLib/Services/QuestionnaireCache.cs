using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.ServiceLib.Services;

public class QuestionnaireCache(IMemoryCache cache, IPathApi api, ILogger<QuestionnaireCache> logger)
{
    public async Task<string?> GetQuestionnaireResourceAsync(String Id, int? Version = null)
    {
        if (string.IsNullOrEmpty(Id)) return null;

        var chachekey = $"qr_{Id}" + (Version.HasValue ? $"_{Version}" : "");

        if( !cache.TryGetValue(cache, out Questionnaire? q))
        {
            logger.LogInformation("loading questionnaire {0}", Id);

            var resp = await api.GetQuestionnaire(Id, Version);
            if (resp.IsSuccessful)
            {
                q = resp.Content;

                var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(5));
                cache.Set(chachekey, q, opts);
            }
            else
            {
                logger.LogWarning("loading questionnaire {0}/{1} failed: {2}, {3}", Id, Version, resp.StatusCode, resp.Error.Message);
            }
        }
        return q?.Resource;
    }
}
