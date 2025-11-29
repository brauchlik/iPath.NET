using iPath.Blazor.ServiceLib.ApiClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace iPath.Blazor.ServiceLib.Services;

public class QuestionnaireCache(IMemoryCache cache, IPathApi api, ILogger<QuestionnaireCache> logger)
{
    public async Task<string> GetQuestionnaireResourceAsync(Guid Id)
    {
        var chachekey = $"qr_{Id}";
        if( !cache.TryGetValue(cache, out string resource))
        {
            logger.LogInformation("loading questionnaire {0}", Id);

            var resp = await api.GetQuestionnaireById(Id);
            if (resp.IsSuccessful)
            {
                resource = resp.Content.Resource;


                var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(5));
                cache.Set(chachekey, resp, opts);
            }
            else
            {
                logger.LogWarning("loading questionnaire {0} failed: {1}, {2}", Id, resp.StatusCode, resp.Error.Message);
            }
        }
        return resource;
    }
}
