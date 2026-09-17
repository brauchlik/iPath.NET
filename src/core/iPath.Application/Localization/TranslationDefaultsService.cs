using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.Application.Localization;

public record TranslationImportResultDto(string Locale, int Added, int Filled);

public record TranslationImportSummaryDto(List<TranslationImportResultDto> Locales)
{
    public bool IsLiveStoreSeparate { get; init; } = true;
    public int TotalAdded => Locales.Sum(x => x.Added);
    public int TotalFilled => Locales.Sum(x => x.Filled);
}

/// <summary>
/// Pushes the shipped baseline (<see cref="LocalizationSettings.DefaultsRoot"/>) into the live store
/// (<see cref="LocalizationSettings.LocalesRoot"/>) - additive only: adds keys missing from the live
/// store and fills keys that exist but are still empty, never overwriting a translated value.
/// </summary>
public class TranslationDefaultsService
{
    private readonly LocalizationSettings _settings;
    private readonly ILogger<TranslationDefaultsService> _logger;
    private readonly LocalizationFileService _shipped;
    private readonly LocalizationFileService _live;

    public TranslationDefaultsService(IOptions<LocalizationSettings> opts, ILoggerFactory loggerFactory)
    {
        _settings = opts.Value;
        _logger = loggerFactory.CreateLogger<TranslationDefaultsService>();
        var fileLogger = loggerFactory.CreateLogger<LocalizationFileService>();

        _shipped = new LocalizationFileService(
            Options.Create(new LocalizationSettings { LocalesRoot = _settings.DefaultsRoot, SupportedCultures = _settings.SupportedCultures }),
            fileLogger);
        _live = new LocalizationFileService(
            Options.Create(new LocalizationSettings { LocalesRoot = _settings.LocalesRoot, SupportedCultures = _settings.SupportedCultures }),
            fileLogger);
    }

    public bool IsLiveStoreSeparate => _settings.IsLiveStoreSeparate;

    public string[] SupportedCultures => _settings.SupportedCultures;

    public TranslationData GetDefaults(string locale) => _shipped.GetTranslationData(locale);

    public TranslationImportSummaryDto Import(string? locale = null)
    {
        var locales = string.IsNullOrEmpty(locale)
            ? _settings.SupportedCultures
            : new[] { locale };

        var results = new List<TranslationImportResultDto>();

        if (!IsLiveStoreSeparate)
        {
            _logger.LogDebug("No separate live translation store configured - import is a no-op");
            foreach (var l in locales)
                results.Add(new(l, 0, 0));
            return new TranslationImportSummaryDto(results) { IsLiveStoreSeparate = false };
        }

        foreach (var l in locales)
        {
            try
            {
                var defaults = _shipped.GetTranslationData(l);
                var target = _live.GetTranslationData(l);
                var (added, filled) = LocalizationKeyScanner.PushDefaults(defaults, target);

                if (added.Count > 0 || filled.Count > 0)
                {
                    _live.SaveTranslation(target);
                    _logger.LogInformation("Translations updated for {Locale}: {Added} new, {Filled} filled from shipped defaults", l, added.Count, filled.Count);
                }

                results.Add(new(l, added.Count, filled.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing shipped default translations for locale {Locale}", l);
                results.Add(new(l, 0, 0));
            }
        }

        return new TranslationImportSummaryDto(results);
    }
}
