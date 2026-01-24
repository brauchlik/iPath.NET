using iPath.Domain.Entities.Base;
using System.Diagnostics;

namespace iPath.Domain.Entities;

[DebuggerDisplay("Quesionnaire {QuestionnaireId}, Version {Version}, active={IsActive}")]
public class QuestionnaireEntity : AuditableEntity
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
    public ICollection<QuestionnaireForCommunity> Communities { get; set; } = [];

    public QuestionnaireSettings Settings { get; set; } = new();
}


public enum eQuestionnaireUsage
{
    None = 0,
    Any = 1,
    CaseDescription = 2,
    Annotation = 3,
    FollowUp = 4,
    FinalAssesment = 5,
}