namespace iPath.Domain.Entities;

public class Annotation : AuditableEntity
{
    public int? ipath2_id { get; set; }

    public Guid? ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }


    public Guid? DcoumentNodeId { get; set; }
    public DocumentNode? Document { get; set; }


    public DateTime CreatedOn { get; set; }

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public AnnotationData? Data { get; set; }

    public ICollection<QuestionnaireResponseEntity> QuestionnaireResponses { get; set; } = [];


    public static Annotation Create(ServiceRequest node, Guid ownerId, AnnotationData data)
    {
        var ret = new Annotation
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            ServiceRequestId = node.Id,
            OwnerId = ownerId,
            Data = data
        };

        // create event

        return ret;
    }
}

public class AnnotationData
{
    public eAnnotationType Type { get; set; } = eAnnotationType.Comment;

    public string? Text { get; set; }

    public CodedConcept? Morphology { get; set; }

    public QuestionnaireResponseData? Questionnaire { get; set; }

}

public enum eAnnotationType
{
    None = 0,
    Comment = 1,
    FinalAssesment = 10,
    FollowUp = 20,
    Note = 30,
    FurtherRequest = 31
}