namespace iPath.Application.Localization;

public interface ILocalizationDataProvider
{
    Task<Result<TranslationData>> GetTranslationDataAsync(string locale);
}
