using DispatchR;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using iPath.Application.Contracts;
using iPath.Application.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace iPath.Blazor.ServiceLib.Services;

public class QuestionnaireCacheServer(IMemoryCache cache, IMediator mediator, ILogger<QuestionnaireCacheServer> logger)
{
    public void ClearCache(string Id, int? Version = null)
    {
        var chachekey = GetKey(Id, Version);
        cache.Remove(chachekey);
    }

    string GetKey(String Id, int? Version = null) => $"qr_{Id}" + (Version.HasValue ? $"_{Version}" : "");

    public async Task<Questionnaire?> GetQuestionnaireAsync(String Id, int? Version = null)
    {
        if (string.IsNullOrEmpty(Id)) return null;

        var chachekey = GetKey(Id, Version);

        if (!cache.TryGetValue(cache, out Questionnaire? q))
        {
            try
            {
                logger.LogInformation("loading questionnaire {0}", Id);

                var entity = await mediator.Send(new GetQuestionnaireQuery(Id, Version), default);
                if (entity is not null)
                {
                    var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                    q = JsonSerializer.Deserialize<Questionnaire>(entity.Resource, options);

                    var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(60));
                    cache.Set(chachekey, q, opts);
                }
                else
                {
                    logger.LogWarning("loading questionnaire {0}/{1} failed", Id, Version);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
            }
        }
        return q;
    }
}
