using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace iPath.Application.Localization;

public class LocalizationFileService
{
    private readonly IOptions<LocalizationSettings> _opts;
    private readonly ILogger<LocalizationFileService> _logger;
    private readonly object _fileLock = new();

    public event Action<string>? TranslationSaved;

    public LocalizationFileService(IOptions<LocalizationSettings> opts, ILogger<LocalizationFileService> logger)
    {
        _logger = logger;
        _opts = opts;
        if (!string.IsNullOrEmpty(_opts.Value.LocalesRoot) && !System.IO.Directory.Exists(_opts.Value.LocalesRoot))
        {
            try
            {
                System.IO.Directory.CreateDirectory(_opts.Value.LocalesRoot);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "LocalesRoot folder could not be created");
            }
        }
    }


    public TranslationData GetTranslationData(string locale)
    {
        lock (_fileLock)
        {
            if (string.IsNullOrEmpty(_opts.Value.LocalesRoot))
            {
                _logger.LogWarning("Translation locale root is not configured, returning empty translation data for {Locale}", locale);
                return EmptyTranslationData(locale);
            }

            if (!_opts.Value.SupportedCultures.Contains(locale))
            {
                _logger.LogWarning("Culture {Locale} is not supported, returning empty translation data", locale);
                return EmptyTranslationData(locale);
            }

            string fileName = Path.Combine(_opts.Value.LocalesRoot, $"{locale}.json");
            if (!File.Exists(fileName))
            {
                _logger.LogWarning("Translation file {FileName} for locale {Locale} does not exist", fileName, locale);
                return EmptyTranslationData(locale);
            }

            try
            {
                var data = JsonSerializer.Deserialize<TranslationData>(File.ReadAllText(fileName));
                if (data is null)
                {
                    _logger.LogWarning("Translation file {FileName} contains no data", fileName);
                    return EmptyTranslationData(locale);
                }
                data.Words ??= new();
                if (string.IsNullOrEmpty(data.locale)) data.locale = locale;
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading translation file {FileName}", fileName);
                return EmptyTranslationData(locale);
            }
        }
    }

    private static TranslationData EmptyTranslationData(string locale) => new()
    {
        locale = locale,
        ModifiedOn = DateTime.Now,
        Words = new()
    };

    public bool SaveTranslation(TranslationData data)
    {
        if (data is null || !IsSupportedLocale(data.locale))
        {
            _logger.LogWarning("Refusing to save translations for unsupported locale {Locale}", data?.locale);
            return false;
        }

        lock (_fileLock)
        {
            return SaveTranslationInternal(data);
        }
    }

    private bool IsSupportedLocale(string? locale)
        => !string.IsNullOrEmpty(locale) && _opts.Value.SupportedCultures.Contains(locale);

    private bool SaveTranslationInternal(TranslationData data)
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
            string fileName = Path.Combine(_opts.Value.LocalesRoot, $"{data.locale}.json");
            File.WriteAllText(fileName, json, System.Text.Encoding.UTF8);
            TranslationSaved?.Invoke(data.locale);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving translation {0}", data.locale);
        }
        return false;
    }

}

public class FileLocalizaitonProvider : ILocalizationDataProvider
{
    private readonly LocalizationFileService _srv;
    public event Action<string>? TranslationDataSaved;

    public FileLocalizaitonProvider(LocalizationFileService srv)
    {
        _srv = srv;
        _srv.TranslationSaved += locale => TranslationDataSaved?.Invoke(locale);
    }

    public async Task<Result<TranslationData>> GetTranslationDataAsync(string locale)
    {
        try
        {
            return new Result<TranslationData>().WithValue(_srv.GetTranslationData(locale));
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public Task<bool> SaveTranslationDataAsync(TranslationData data)
    {
        try
        {
            return Task.FromResult(_srv.SaveTranslation(data));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}