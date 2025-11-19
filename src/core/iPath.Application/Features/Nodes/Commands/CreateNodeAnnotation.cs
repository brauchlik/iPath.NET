namespace iPath.Application.Features.Nodes;

public record CreateNodeAnnotationCommand(Guid NodeId, string? Text, string? QuestionnaireResponse)
    : IRequest<CreateNodeAnnotationCommand, Task<AnnotationDto>>
    , IEventInput
{
    public string ObjectName => nameof(Node);
}
