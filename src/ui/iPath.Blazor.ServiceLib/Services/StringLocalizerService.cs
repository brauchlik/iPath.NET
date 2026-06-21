using iPath.Application.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace iPath.Blazor.ServiceLib.Services;

public class StringLocalizerService : IStringLocalizer
{
    private readonly ILocalizationDataProvider _provider;
    private readonly IOptions<LocalizationSettings> _opts;
    private readonly ILogger<StringLocalizerService> _logger;
    private readonly Dictionary<string, TranslationData> _translationsData = new();

    public StringLocalizerService(
        ILocalizationDataProvider provider, 
        IOptions<LocalizationSettings> opts, 
        ILogger<StringLocalizerService> logger) 
    {
        _provider = provider;
        _opts = opts;
        _logger = logger;
        
        _provider.TranslationDataSaved += locale =>
        {
            lock (_translationsData)
            {
                _translationsData.Remove(locale);
            }
        };
    }

    public bool AddMissingTranslations { get; set; } = true;
    public bool IsModified { get; private set; }

    public async Task<TranslationData> LoadTranslationData(string locale, bool reload = false)
    {
        if (reload)
        {
            lock (_translationsData)
            {
                if (_translationsData.ContainsKey(locale))
                {
                    _translationsData.Remove(locale);
                }
            }
        }

        bool containsLocale;
        lock (_translationsData)
        {
            containsLocale = _translationsData.ContainsKey(locale);
        }

        if (!containsLocale)
        {
            try
            {
                var resp = await _provider.GetTranslationDataAsync(locale);
                lock (_translationsData)
                {
                    if (!_translationsData.ContainsKey(locale))
                    {
                        if (resp.IsSuccess)
                        {
                            _translationsData.Add(locale, resp.Value);
                        }
                        else
                        {
                            _translationsData.Add(locale, new TranslationData { locale = locale, Words = new() });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loading Translations Error {Locale}", locale);
            }
        }

        // Ensure English master data is cached so GetTranslation can backfill keys
        if (locale != "en")
        {
            await EnsureEnglishCacheAsync();
        }

        lock (_translationsData)
        {
            return _translationsData[locale];
        }
    }

    private async Task EnsureEnglishCacheAsync()
    {
        bool hasEn;
        lock (_translationsData) { hasEn = _translationsData.ContainsKey("en"); }
        if (hasEn) return;

        try
        {
            var resp = await _provider.GetTranslationDataAsync("en");
            lock (_translationsData)
            {
                if (!_translationsData.ContainsKey("en"))
                {
                    _translationsData["en"] = resp.IsSuccess ? resp.Value : new TranslationData { locale = "en", Words = new() };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preloading English master translation data");
            lock (_translationsData)
            {
                if (!_translationsData.ContainsKey("en"))
                    _translationsData["en"] = new TranslationData { locale = "en", Words = new() };
            }
        }
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
        
        if (currentLocale != "en" && _translationsData.TryGetValue(currentLocale, out var data))
        {
            if (data.Words != null)
            { 
                if (data.Words.TryGetValue(key, out var value))
                {
                    string trans = string.IsNullOrEmpty(value) ? key : value;
                    return new LocalizedString(key, trans, false);
                }
                else if (_opts.Value.Active && _opts.Value.AddMissingStrings)
                {
                    try
                    {
                        data.Words.TryAdd(key, "");
                        IsModified = true;
                        
                        if (_opts.Value.AutoSave)
                        {
                            // Backfill English master key list so all locales see this key for translation
                            if (_translationsData.TryGetValue("en", out var enData))
                            {
                                enData.Words.TryAdd(key, key);
                                _ = _provider.SaveTranslationDataAsync(enData);
                            }
                            _ = _provider.SaveTranslationDataAsync(data);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error adding/saving missing translation key '{Key}' for locale '{Locale}'", key, currentLocale);
                    }
                }
            }
            else
            {
                _logger.LogWarning("Localization for {Locale} contains no words", currentLocale);
            }
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
