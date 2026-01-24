namespace iPath.Domain.Entities;

public class QuestionnaireResponseData
{
    public string QuestionnaireId { get; set; }
    public int? Version { get; set; }
    public string? Resource { get; set; }
    public string? GeneratedText { get; set; }
}
