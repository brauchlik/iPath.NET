using iPath.Domain.Entities.Base;

namespace iPath.Domain.Entities;

public class QuestionnaireForCommunity
{
    public Guid Id { get; set; }

    public Guid QuestionnaireId { get; set; }
    public QuestionnaireEntity Questionnaire { get; set; }

    public Guid CommunityId { get; set; }
    public Community Community { get; set; }

    public eQuestionnaireUsage Usage { get; set; }
    public int? ExplicitVersion { get; set; } = null;

    public ConceptFilter? BodySiteFilter { get; set; }
}
