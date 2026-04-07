namespace iPath.Application.Features.Annotations;

public record UpdateAnnotationCommand(Guid annotationId, AnnotationData? Data)
    : IRequest<UpdateAnnotationCommand, Task<AnnotationDto>>
    , IEventInput
{
    public string ObjectName => nameof(Annotation);
}
