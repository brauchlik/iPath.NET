using iPath.Application.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;


namespace iPath.Blazor.ServiceLib.Services;

public class StringLocalizerService(ILocalizationDataProvider data, ILogger<StringLocalizerService> logger) : IStringLocalizer
{
    private Dictionary<string, TranslationData> _translationsData = new();

    public bool AddMissingtTranslations { get; set; } = true;
    public bool IsModified { get; private set; }

    public async Task<TranslationData> LoadTranslationData(string locale, bool reload = false)
    {
        if (reload && _translationsData.ContainsKey(locale))
        {
            _translationsData.Remove(locale);
        }

        if (!_translationsData.ContainsKey(locale))
        {
            try
            {
                var resp = await data.GetTranslationDataAsync(locale);
                if (resp.IsSuccess)
                {
                    _translationsData.Add(locale, resp.Value);
                }
                else
                {
                    _translationsData.Add(locale, new TranslationData());
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Loading Translations Error {locale}", ex);
            }
        }
        return _translationsData[locale];
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
            logger.LogError(ex.Message, ex);
        }
        return ret;
    }


    private LocalizedString GetTranslation(string key)
    {
        if (_translationsData.ContainsKey(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName))
        {
            var data = _translationsData[System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName];
            if (data.Words != null)
            { 
                if (data.Words.ContainsKey(key))
                {
                    string trans = string.IsNullOrEmpty(data.Words[key]) ? key : data.Words[key];
                    return new LocalizedString(key, trans, false);
                }
                else if (AddMissingtTranslations)
                {
                    try
                    {
                        data.Words.TryAdd(key, "");
                        IsModified = true;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.Message, ex);
                    }
                }
            }
            else
            {
                logger.LogWarning("localization for {0} contains no words", System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            }
        }
        return new LocalizedString(key, key, true);
    }


    public LocalizedString this[string name] => GetTranslation(name);


    public LocalizedString this[string name, params object[] arguments] => GetTranslation(name, arguments);


    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var _localizedStrings = new List<LocalizedString>();

        if (_translationsData.ContainsKey(System.Globalization.CultureInfo.CurrentUICulture.Name))
        {
            foreach (var trans in _translationsData[System.Globalization.CultureInfo.CurrentUICulture.Name].Words)
            {
                _localizedStrings.Add(new LocalizedString(trans.Key, trans.Value));
            }
        }

        return _localizedStrings;
    }
}
