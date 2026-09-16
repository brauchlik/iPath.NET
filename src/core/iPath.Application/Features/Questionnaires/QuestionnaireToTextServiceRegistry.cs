namespace iPath.Application.Features.Questionnaires;

public class QuestionnaireToTextServiceRegistry(string? defaultServiceKey = null) : IQuestionnaireToTextServiceRegistry
{
    private static readonly TextPreviewServiceInfo[] Services =
    [
        new("Default List", "Default (List)", "Question-by-question list with HTML formatting"),
        new("CSV", "CSV", "Comma-separated question = answer pairs"),
        new("Case Description (Compact)", "Case Description (Compact)", "Grouped by section, sub-items in brackets"),
        new("Case Description (Expanded)", "Case Description (Expanded)", "Grouped by section, sub-items on separate lines"),
    ];

    public IReadOnlyList<TextPreviewServiceInfo> GetAll() => Services;

    // configured default if it names a known service, otherwise the first entry
    public TextPreviewServiceInfo GetDefault() =>
        Services.FirstOrDefault(s => string.Equals(s.Key, defaultServiceKey, StringComparison.OrdinalIgnoreCase))
        ?? Services[0];
}
