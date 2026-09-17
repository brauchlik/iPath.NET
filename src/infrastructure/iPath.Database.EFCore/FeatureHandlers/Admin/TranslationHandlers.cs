using iPath.Application.Features.Admin;
using iPath.Application.Localization;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetTranslationStatusHandler(
    LocalizationFileService localizationFileService,
    TranslationDefaultsService defaultsService,
    ILogger<GetTranslationStatusHandler> logger)
    : IRequestHandler<GetTranslationStatusQuery, Task<TranslationStatusDto>>
{
    public Task<TranslationStatusDto> Handle(GetTranslationStatusQuery request, CancellationToken ct)
    {
        var dto = new TranslationStatusDto
        {
            Locale = request.Locale,
            IsEditable = defaultsService.IsLiveStoreSeparate
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

            // The shipped baseline, so the page can flag where the local translation differs.
            // Identical to the live values when no separate store is configured, so skip it then.
            TranslationData? shippedData = dto.IsEditable ? defaultsService.GetDefaults(request.Locale) : null;

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

                if (shippedData?.Words != null &&
                    shippedData.Words.TryGetValue(key, out var shippedValue) &&
                    !string.IsNullOrWhiteSpace(shippedValue))
                {
                    dto.Defaults[key] = shippedValue;
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
    TranslationDefaultsService defaultsService,
    ILogger<UpdateTranslationKeyHandler> logger)
    : IRequestHandler<UpdateTranslationKeyCommand, Task<bool>>
{
    public Task<bool> Handle(UpdateTranslationKeyCommand request, CancellationToken ct)
    {
        if (!defaultsService.IsLiveStoreSeparate)
        {
            logger.LogWarning("Refusing translation update for {Locale}: no separate live translation store is configured", request.Locale);
            return Task.FromResult(false);
        }

        if (!defaultsService.SupportedCultures.Contains(request.Locale))
        {
            logger.LogWarning("Refusing translation update for unsupported locale {Locale}", request.Locale);
            return Task.FromResult(false);
        }

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

public class ImportTranslationDefaultsHandler(
    TranslationDefaultsService defaultsService,
    ILogger<ImportTranslationDefaultsHandler> logger)
    : IRequestHandler<ImportTranslationDefaultsCommand, Task<TranslationImportSummaryDto>>
{
    public Task<TranslationImportSummaryDto> Handle(ImportTranslationDefaultsCommand request, CancellationToken ct)
    {
        if (!defaultsService.IsLiveStoreSeparate)
        {
            logger.LogWarning("Import of shipped default translations skipped: no separate live translation store is configured");
        }

        return Task.FromResult(defaultsService.Import(request.Locale));
    }
}
