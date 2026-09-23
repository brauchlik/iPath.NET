namespace iPath.Domain.Entities;

public class QuestionnaireSettings
{
    public string? Filename { get; set; }
    public ConceptFilter? BodySiteFilter { get; set; }
    public string? TextPreviewService { get; set; }

    /// <summary>
    /// Opt-out flag. null (not configured) means answers are extracted - only an explicit false
    /// switches extraction off, so questionnaires stored before this property existed keep working.
    /// </summary>
    public bool? ExtractAnswers { get; set; }

    public bool ShouldExtractAnswers() => ExtractAnswers != false;

    /// <summary>
    /// Which viewer renders this questionnaire. null (not configured) means LForms, so
    /// questionnaires stored before this setting existed keep their current behavior.
    /// </summary>
    public QuestionnaireRenderer? PreferredRenderer { get; set; }
}
