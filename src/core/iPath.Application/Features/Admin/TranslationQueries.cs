using iPath.Application.Localization;

namespace iPath.Application.Features.Admin;

public record GetTranslationStatusQuery(string Locale)
    : IRequest<GetTranslationStatusQuery, Task<TranslationStatusDto>>;

public record UpdateTranslationKeyCommand(string Locale, string Key, string Translation)
    : IRequest<UpdateTranslationKeyCommand, Task<bool>>;

public record ImportTranslationDefaultsCommand(string? Locale = null)
    : IRequest<ImportTranslationDefaultsCommand, Task<TranslationImportSummaryDto>>;

public class TranslationStatusDto
{
    public string Locale { get; set; } = string.Empty;
    public int TotalKeys { get; set; }
    public int TranslatedKeys { get; set; }
    public int MissingKeys { get; set; }
    public List<string> UntranslatedKeys { get; set; } = new();
    public Dictionary<string, string> Words { get; set; } = new();
    public Dictionary<string, TranslationMetadata> WordMetadata { get; set; } = new();

    /// <summary>Shipped value per key, for showing where the local translation differs.</summary>
    public Dictionary<string, string> Defaults { get; set; } = new();

    /// <summary>False when no separate live store is configured - the page is read-only then.</summary>
    public bool IsEditable { get; set; } = true;
}
