using iPath.Application.Features.Admin;
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
    private readonly ITranslationJobQueue _translationJobQueue;
    private readonly ConcurrentDictionary<string, TranslationData> _translationsData = new();

    public StringLocalizerService(
        ILocalizationDataProvider provider, 
        IOptions<LocalizationSettings> opts, 
        ILogger<StringLocalizerService> logger,
        ITranslationJobQueue translationJobQueue) 
    {
        _provider = provider;
        _opts = opts;
        _logger = logger;
        _translationJobQueue = translationJobQueue;
        
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

    public bool AddMissingTranslations { get; set; } = true;
    public bool IsModified { get; private set; }

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

        // Ensure English master data is cached so GetTranslation can backfill keys
        if (locale != "en")
        {
            await EnsureEnglishCacheAsync();
        }

        return _translationsData.TryGetValue(locale, out var data) 
            ? data 
            : new TranslationData { locale = locale, Words = new() };
    }

    private async Task EnsureEnglishCacheAsync()
    {
        if (_translationsData.ContainsKey("en")) return;

        try
        {
            var resp = await _provider.GetTranslationDataAsync("en");
            if (!_translationsData.ContainsKey("en"))
            {
                _translationsData["en"] = resp.IsSuccess ? resp.Value : new TranslationData { locale = "en", Words = new() };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preloading English master translation data");
            if (!_translationsData.ContainsKey("en"))
                _translationsData["en"] = new TranslationData { locale = "en", Words = new() };
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
                                if (enData.Words.TryAdd(key, key))
                                {
                                    _translationJobQueue.EnqueueKey(key);
                                    _ = _provider.SaveTranslationDataAsync(enData);
                                }
                            }
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
