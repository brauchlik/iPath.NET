namespace iPath.Application.Localization;

public interface ITranslationLoader
{
    Task<TranslationData> LoadTranslationData(string locale, bool reload = false);
}
