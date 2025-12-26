namespace iPath.Domain.Entities;

public class Annotation : AuditableEntity
{
    public int? ipath2_id { get; set; }

    public Guid? NodeId { get; set; }
    public Node? Node { get; set; }


    public DateTime CreatedOn { get; set; }

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public string? Text { get; set; }

    public AnnotationData? Data { get; set; }

    public ICollection<QuestionnaireResponse> QuestionnaireResponses { get; set; } = [];


    public static Annotation Create(Node node, Guid ownerId, string text, AnnotationData data)
    {
        var ret = new Annotation
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            NodeId = node.Id,
            OwnerId = ownerId,
            Text = text,
            Data = data
        };

        // create event

        return ret;
    }
}

public class AnnotationData
{
    public CodedConcept Morphology { get; set; }
}