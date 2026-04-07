namespace iPath.Blazor.Componenents.ServiceRequests;

public class AnnotationEditModel
{
    public Guid? Id { get; set; } = null;
    public bool AskMorphology { get; set; }

    public AnnotationData Data { get; set; } = new();

    public Guid ServiceRequestId { get; set; }

    public static AnnotationEditModel FromDto(AnnotationDto dto)
    {
        var m = new AnnotationEditModel
        {
            Id = dto.Id,
            ServiceRequestId = dto.ServiceRequestId,
            Data = dto.Data
        };
        return m;
    }
}
           