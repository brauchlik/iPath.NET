namespace iPath.Application.Features.Nodes;

public record DeleteNodeAnnotationCommand(Guid AnnotationId)
    : IRequest<DeleteNodeAnnotationCommand, Task<Guid>>
    , IEventInput
{
    public string ObjectName => nameof(Node);
}

