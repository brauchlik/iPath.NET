using System.Diagnostics;

namespace iPath.Domain.Entities;

[DebuggerDisplay("Quesionnaire {QuestionnaireId}, Version {Version}, active={IsActive}")]
public class Questionnaire : AuditableEntity
{
    [Required]
    public required string QuestionnaireId { get; set; }

    public required string Name { get; set; }

    public int Version { get; set; }
    public bool IsActive { get; set; }

    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }

    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }

    public required string Resource { get; set; }

    public ICollection<QuestionnaireForGroup> Groups { get; set; } = [];
}


public class QuestionnaireForGroup
{
    public Guid Id { get; set; }

    public Guid QuestionnaireId { get; set; }
    public Questionnaire Questionnaire { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; }

    public eQuestionnaireUsage Usage { get; set; }
    public int? ExplicitVersion { get; set; } = null;
}


public enum eQuestionnaireUsage
{
    None = 0,
    Any = 1,
    CaseDescription = 2,
    Annotation = 3,
    FollowUp = 4,
    FinalDiagnosis = 5,
}