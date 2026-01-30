namespace iPath.Domain.Entities;

public class QuestionnaireForGroup
{
    public Guid Id { get; set; }

    public Guid QuestionnaireId { get; set; }
    public QuestionnaireEntity Questionnaire { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; }

    public eQuestionnaireUsage Usage { get; set; }
    public int? ExplicitVersion { get; set; } = null;

    public int Priority { get; set; }
}
