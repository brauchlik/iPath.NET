namespace iPath.Application.Features.Questionnaires;

public class QuestionnaireToTextServiceRegistry : IQuestionnaireToTextServiceRegistry
{
    private static readonly TextPreviewServiceInfo[] Services =
    [
        new("Default List", "Default (List)", "Question-by-question list with HTML formatting"),
        new("CSV", "CSV", "Comma-separated question = answer pairs"),
        new("Case Description (Compact)", "Case Description (Compact)", "Grouped by section, sub-items in brackets"),
        new("Case Description (Expanded)", "Case Description (Expanded)", "Grouped by section, sub-items on separate lines"),
    ];

    private static readonly TextPreviewServiceInfo DefaultService = Services[0];

    public IReadOnlyList<TextPreviewServiceInfo> GetAll() => Services;

    public TextPreviewServiceInfo GetDefault() => DefaultService;
}
