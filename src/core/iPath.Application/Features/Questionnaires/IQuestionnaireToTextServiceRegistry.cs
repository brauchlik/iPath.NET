namespace iPath.Application.Features.Questionnaires;

public record TextPreviewServiceInfo(string Key, string DisplayName, string? Description = null);

public interface IQuestionnaireToTextServiceRegistry
{
    IReadOnlyList<TextPreviewServiceInfo> GetAll();
    TextPreviewServiceInfo? GetDefault();
}
