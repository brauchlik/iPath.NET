using iPath.Application.Localization;

namespace iPath.Application.Features.Admin;

public record GetTranslationStatusQuery(string Locale)
    : IRequest<GetTranslationStatusQuery, Task<TranslationStatusDto>>;

public record TranslateKeysBatchCommand(string Locale, List<string> Keys)
    : IRequest<TranslateKeysBatchCommand, Task<TranslationResultDto>>;

public record UpdateTranslationKeyCommand(string Locale, string Key, string Translation)
    : IRequest<UpdateTranslationKeyCommand, Task<bool>>;

public class TranslationStatusDto
{
    public string Locale { get; set; } = string.Empty;
    public int TotalKeys { get; set; }
    public int TranslatedKeys { get; set; }
    public int MissingKeys { get; set; }
    public List<string> UntranslatedKeys { get; set; } = new();
    public Dictionary<string, string> Words { get; set; } = new();
    public Dictionary<string, TranslationMetadata> WordMetadata { get; set; } = new();
}

public class TranslationResultDto
{
    public string Locale { get; set; } = string.Empty;
    public int TranslatedCount { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> SuccessfulKeys { get; set; } = new();
    public List<string> FailedKeys { get; set; } = new();
}
