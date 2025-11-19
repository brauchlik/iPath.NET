using iPath.Application.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace iPath.RazorLib.Localization;

public class LocalizationFileService(IOptions<LocalizationSettings> opts, ILogger<LocalizationFileService> logger)
{
    public TranslationData GetTranslationData(string locale)
    {
        TranslationData data;

        if (!opts.Value.SupportedCultures.Contains(locale))
        {
            throw new InvalidOperationException($"Culture {locale} is not supported");
        }

        string fileName = Path.Combine(opts.Value.LocalesRoot, $"{locale}.json");
        if (!File.Exists(fileName))
        {
            data = new();
            data.locale = locale;
            data.ModifiedOn = DateTime.Now;
            data.Words = new();
            data.Words["Test"] = "Test";
            data.Words["Test2"] = "Test2";
            if (opts.Value.AutoSave) SaveTranslation(data);
        }
        else
        {
            data = JsonSerializer.Deserialize<TranslationData>(File.ReadAllText(fileName));
        }

        return data;
    }

    public bool SaveTranslation(TranslationData data)
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

}
