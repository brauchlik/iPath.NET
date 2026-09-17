using iPath.Application.Features.Admin;
using iPath.Application.Localization;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetTranslationStatusHandler(
    LocalizationFileService localizationFileService,
    ILogger<GetTranslationStatusHandler> logger)
    : IRequestHandler<GetTranslationStatusQuery, Task<TranslationStatusDto>>
{
    public Task<TranslationStatusDto> Handle(GetTranslationStatusQuery request, CancellationToken ct)
    {
        var dto = new TranslationStatusDto
        {
            Locale = request.Locale
        };

        try
        {
            // Load master key list (en.json) and the target locale
            var enData = localizationFileService.GetTranslationData("en");
            var localeData = localizationFileService.GetTranslationData(request.Locale);

            // Use en.json keys as the authoritative baseline of all keys that need translation
            var allKeys = new HashSet<string>(enData?.Words?.Keys ?? Enumerable.Empty<string>());

            // Also include any locale-specific keys not yet in en.json (backward compat)
            if (localeData?.Words != null)
            {
                foreach (var key in localeData.Words.Keys)
                {
                    allKeys.Add(key);
                }
            }

            dto.TotalKeys = allKeys.Count;

            foreach (var key in allKeys)
            {
                if (localeData?.Words != null &&
                    localeData.Words.TryGetValue(key, out var value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    // Key is fully translated in the locale
                    dto.TranslatedKeys++;
                    dto.Words[key] = value;
                }
                else
                {
                    // Key is missing or empty in the locale
                    dto.MissingKeys++;
                    dto.UntranslatedKeys.Add(key);
                    dto.Words[key] = string.Empty;
                }

                // Copy metadata if available
                if (localeData?.WordMetadata != null &&
                    localeData.WordMetadata.TryGetValue(key, out var meta))
                {
                    dto.WordMetadata[key] = meta;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting translation status for {Locale}", request.Locale);
        }

        return Task.FromResult(dto);
    }
}

public class UpdateTranslationKeyHandler(
    LocalizationFileService localizationFileService,
    ILogger<UpdateTranslationKeyHandler> logger)
    : IRequestHandler<UpdateTranslationKeyCommand, Task<bool>>
{
    public Task<bool> Handle(UpdateTranslationKeyCommand request, CancellationToken ct)
    {
        try
        {
            var data = localizationFileService.GetTranslationData(request.Locale);
            if (data?.Words != null)
            {
                data.Words[request.Key] = request.Translation.Trim();
                data.WordMetadata[request.Key] = new TranslationMetadata
                {
                    ModelUsed = "Human",
                    TranslatedAt = DateTime.UtcNow,
                    IsHumanModified = true
                };
                bool saved = localizationFileService.SaveTranslation(data);
                return Task.FromResult(saved);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating translation key '{Key}' for locale '{Locale}'", request.Key, request.Locale);
        }
        return Task.FromResult(false);
    }
}
