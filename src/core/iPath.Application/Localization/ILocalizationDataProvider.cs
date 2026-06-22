namespace iPath.Application.Localization;

public interface ILocalizationDataProvider
{
    Task<Result<TranslationData>> GetTranslationDataAsync(string locale);
    Task<bool> SaveTranslationDataAsync(TranslationData data);
    event Action<string>? TranslationDataSaved;
}
