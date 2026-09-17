namespace iPath.Domain.Entities;

public class QuestionnaireResponseData
{
    public string QuestionnaireId { get; set; }
    public int? Version { get; set; }
    public string? Resource { get; set; }
    public string? GeneratedText { get; set; }

    public DateTime? ExtractedOn { get; set; }
    public int? ExtractionVersion { get; set; }
    public int? ExtractedAnswerCount { get; set; }
    public string? ExtractionError { get; set; }

    /// <summary>Switched off for this questionnaire - distinct from "extracted, nothing answered".</summary>
    public bool ExtractionDisabled { get; set; }
}
