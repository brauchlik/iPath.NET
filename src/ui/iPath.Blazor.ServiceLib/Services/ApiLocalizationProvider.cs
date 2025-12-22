using FluentResults;
using iPath.Application.Localization;
using iPath.Blazor.ServiceLib.ApiClient;

namespace iPath.Blazor.ServiceLib.Services;

public class ApiLocalizationProvider(IPathApi api) : ILocalizationDataProvider
{
    public async Task<Result<TranslationData>> GetTranslationDataAsync(string locale)
    {
        var resp = await api.GetTranslations(locale);
        return resp.ToResult();
    }
}
