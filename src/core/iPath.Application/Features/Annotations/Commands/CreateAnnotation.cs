namespace iPath.Application.Features.Annotations;

public record CreateAnnotationCommand(Guid requestId, AnnotationData? Data, Guid? ReplyToId = null)
    : IRequest<CreateAnnotationCommand, Task<AnnotationDto>>
    , IEventInput
{
    public string ObjectName => nameof(Annotation);
}


public static partial class ServiceRequestCommandExtensions
{
    public static Annotation CreateNodeAnnotation(this ServiceRequest node, CreateAnnotationCommand request, Guid userId)
    {
        var a = new Annotation
        {
            Data = request.Data,
            OwnerId = userId,
            ServiceRequestId = request.requestId,
            DcoumentNodeId = request.Data.DocumentId,
            CreatedOn = DateTime.UtcNow,
            ReplyToId = request.ReplyToId
        };
        node.Annotations.Add(a);
        node.CreateEvent<AnnotationAddedEvent, CreateAnnotationCommand>(request, userId);
        return a;
    }
}

