namespace iPath.Domain.Entities;

public class QuestionnaireResponse : AuditableEntity
{
    public Guid QuestionnaireId { get; set; }
    public required Questionnaire Questionnaire { get; set; }

    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }

    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }

    public Guid? NodeId { get; set; }
    public Node? Node { get; set; }

    public Guid? AnnotationId { get; set; }
    public Annotation? Annotation { get; set; }

    public required string Resource { get; set; }
}

