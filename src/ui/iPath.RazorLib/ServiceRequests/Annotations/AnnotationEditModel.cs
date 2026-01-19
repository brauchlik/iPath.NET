namespace iPath.Blazor.Componenents.ServiceRequests;

public class AnnotationEditModel
{
    public bool AskMorphology { get; set; }

    public AnnotationData Data { get; set; } = new();

    public Guid ServiceRequestId { get; set; }
    public Guid? DocumentId { get; set; }

}
           