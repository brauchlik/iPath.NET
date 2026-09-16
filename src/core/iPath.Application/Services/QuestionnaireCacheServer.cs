using DispatchR;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using iPath.Application.Contracts;
using iPath.Application.Features;
using iPath.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace iPath.Application.Services;

public class QuestionnaireCacheServer(IMemoryCache cache, IMediator mediator, ILogger<QuestionnaireCacheServer> logger)
{
    public void ClearCache(string Id, int? Version = null)
    {
        var cacheKey = GetKey(Id, Version);
        cache.Remove(cacheKey);
    }

    string GetKey(String Id, int? Version = null) => $"qr_{Id}" + (Version.HasValue ? $"_{Version}" : "");
    string GetSettingsKey(String Id, int? Version = null) => $"qr_settings_{Id}" + (Version.HasValue ? $"_{Version}" : "");

    public async Task<Questionnaire?> GetQuestionnaireAsync(String Id, int? Version = null)
    {
        if (string.IsNullOrEmpty(Id)) return null;

        var cacheKey = GetKey(Id, Version);

        if (!cache.TryGetValue(cacheKey, out Questionnaire? q))
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
                    cache.Set(cacheKey, q, opts);

                    // Cache settings alongside the questionnaire
                    var settingsCacheKey = GetSettingsKey(Id, Version);
                    cache.Set(settingsCacheKey, entity.Settings ?? new QuestionnaireSettings(), opts);
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

    public async Task<QuestionnaireSettings?> GetSettingsAsync(String Id, int? Version = null)
    {
        if (string.IsNullOrEmpty(Id)) return null;

        var settingsCacheKey = GetSettingsKey(Id, Version);

        if (cache.TryGetValue(settingsCacheKey, out QuestionnaireSettings? settings))
            return settings;

        // Settings might not be cached yet; load the questionnaire to populate cache
        await GetQuestionnaireAsync(Id, Version);

        cache.TryGetValue(settingsCacheKey, out settings);
        return settings;
    }
}
