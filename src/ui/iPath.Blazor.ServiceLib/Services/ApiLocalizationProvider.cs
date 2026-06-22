using FluentResults;
using iPath.Application.Localization;
using iPath.Blazor.ServiceLib.ApiClient;

namespace iPath.Blazor.ServiceLib.Services;

public class ApiLocalizationProvider(IPathApi api) : ILocalizationDataProvider
{
    public event Action<string>? TranslationDataSaved;

    public async Task<Result<TranslationData>> GetTranslationDataAsync(string locale)
    {
        var resp = await api.GetTranslations(locale);
        return resp.ToResult();
    }

    public async Task<bool> SaveTranslationDataAsync(TranslationData data)
    {
        var missingKeys = data.Words.Where(w => string.IsNullOrEmpty(w.Value)).Select(w => w.Key).ToList();
        if (missingKeys.Count == 0) return true;

        var resp = await api.AddMissingKeys(data.locale, missingKeys);
        bool success = resp.IsSuccessful && resp.Content;
        if (success)
        {
            TranslationDataSaved?.Invoke(data.locale);
        }
        return success;
    }
}
