namespace iPath.Domain.Entities;

public class AnnotationEvent : EventEntity
{
    [JsonIgnore]
    public Annotation Annotation { get; set; }
}
