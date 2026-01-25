using iPath.Domain.Entities.Base;

namespace iPath.Domain.Entities;

public class QuestionnaireSettings
{
    public string? Filename { get; set; }
    public ConceptFilter? BodySiteFilter { get; set; }
}
