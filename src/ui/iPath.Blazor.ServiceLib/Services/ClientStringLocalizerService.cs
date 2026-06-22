using iPath.Application.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace iPath.Blazor.ServiceLib.Services;

public class ClientStringLocalizerService : IStringLocalizer
{
    private readonly ILocalizationDataProvider _provider;
    private readonly IOptions<LocalizationSettings> _opts;
    private readonly ILogger<ClientStringLocalizerService> _logger;
    private readonly ConcurrentDictionary<string, TranslationData> _translationsData = new();

    public ClientStringLocalizerService(
        ILocalizationDataProvider provider,
        IOptions<LocalizationSettings> opts,
        ILogger<ClientStringLocalizerService> logger)
    {
        _provider = provider;
        _opts = opts;
        _logger = logger;

        _provider.TranslationDataSaved += async locale =>
        {
            try
            {
                await LoadTranslationData(locale, reload: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload translation data for locale {Locale}", locale);
            }
        };
    }

    public async Task<TranslationData> LoadTranslationData(string locale, bool reload = false)
    {
        if (reload || !_translationsData.ContainsKey(locale))
        {
            try
            {
                var resp = await _provider.GetTranslationDataAsync(locale);
                if (resp.IsSuccess)
                {
                    _translationsData[locale] = resp.Value;
                }
                else
                {
                    _translationsData.TryAdd(locale, new TranslationData { locale = locale, Words = new() });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loading Translations Error {Locale}", locale);
            }
        }

        return _translationsData.TryGetValue(locale, out var data)
            ? data
            : new TranslationData { locale = locale, Words = new() };
    }

    private LocalizedString GetTranslation(string key, params object[] args)
    {
        var ret = GetTranslation(key);
        try
        {
            return new LocalizedString(key, string.Format(ret.Value, args), ret.ResourceNotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Formatting Translation Error for key '{Key}'", key);
        }
        return ret;
    }

    private LocalizedString GetTranslation(string key)
    {
        var currentLocale = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        if (currentLocale != "en" && _translationsData.TryGetValue(currentLocale, out var data) && data.Words?.TryGetValue(key, out var value) == true)
        {
            string trans = string.IsNullOrEmpty(value) ? key : value;
            return new LocalizedString(key, trans, false);
        }

        return new LocalizedString(key, key, true);
    }

    public LocalizedString this[string name] => GetTranslation(name);

    public LocalizedString this[string name, params object[] arguments] => GetTranslation(name, arguments);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var localizedStrings = new List<LocalizedString>();
        var currentLocale = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        if (_translationsData.TryGetValue(currentLocale, out var data) && data.Words != null)
        {
            foreach (var trans in data.Words)
            {
                localizedStrings.Add(new LocalizedString(trans.Key, trans.Value));
            }
        }

        return localizedStrings;
    }
}
