using iPath.Application.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace iPath.RazorLib.Localization;

public class LocalizationService(IOptions<LocalizationSettings> opts, ILogger<LocalizationService> logger) 
    : IStringLocalizer
{
    // private static Dictionary<string, Dictionary<string, LocalizedString>>? _translations = null!;

    private Dictionary<string, TranslationData> _translationsData = new();

    private TranslationData GetTranslationData(string locale)
    {
        if (!_translationsData.ContainsKey(locale))
        {
            // check directory
            if (!System.IO.Directory.Exists(opts.Value.LocalesRoot))
            {
                logger.LogWarning("Locales directory not found: {0}", opts.Value.LocalesRoot);
                _translationsData.Add(locale, new TranslationData());
            }
            else
            {
                string fileName = Path.Combine(opts.Value.LocalesRoot, $"{locale}.json");

                if (!File.Exists(fileName))
                {
                    TranslationData newItem = new();
                    newItem.locale = locale;
                    newItem.ModifiedOn = DateTime.Now;
                    newItem.Words = new();
                    _translationsData.Add(locale, newItem);
                    if (opts.Value.AutoSave) SaveTranslation(newItem);
                }
                else
                {
                    var data = JsonSerializer.Deserialize<TranslationData>(File.ReadAllText(fileName));
                    _translationsData.Add(locale, data);
                }
            }
        }
        return _translationsData[locale];
    }

    public int SaveTranslations()
    {
        int count = 0;
        if (_translationsData != null)
        {
            foreach (var t in _translationsData.Values)
            {
                if (SaveTranslation(t)) { count++; }
            }
        }
        return count;
    }

    private bool SaveTranslation(TranslationData data)
    {
        try
        {
            data.ModifiedOn = DateTime.Now;
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(data, options);
            string fileName = Path.Combine(opts.Value.LocalesRoot, $"{data.locale}.json");
            File.WriteAllText(fileName, json, System.Text.Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while saving translation {0}", data.locale);
        }
        return false;
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
        }
        return ret;
    }


    private LocalizedString GetTranslation(string key)
    {
        if (opts.Value.Active && System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "en")
        {
            var data = GetTranslationData(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            if (data.Words.ContainsKey(key))
            {
                string trans = string.IsNullOrEmpty(data.Words[key]) ? key : data.Words[key];
                return new LocalizedString(key, trans, false);
            }
            else if (opts.Value.AddMissingStrings)
            {
                data.Words.TryAdd(key, "");
                if (opts.Value.AutoSave) SaveTranslation(data);
            }
        }

        return new LocalizedString(key, key, true);
    }


    public LocalizedString this[string name] => GetTranslation(name);


    public LocalizedString this[string name, params object[] arguments] => GetTranslation(name, arguments);


    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var _localizedStrings = new List<LocalizedString>();

        foreach (var trans in GetTranslationData(System.Globalization.CultureInfo.CurrentUICulture.Name).Words)
        {
            _localizedStrings.Add(new LocalizedString(trans.Key, trans.Value));
        }

        return _localizedStrings;
    }
}
