namespace iPath.Application.Features.Annotations;

public record DeleteAnnotationCommand(Guid AnnotationId)
    : IRequest<DeleteAnnotationCommand, Task<Guid>>
    , IEventInput
{
    public string ObjectName => nameof(Annotation);
}

