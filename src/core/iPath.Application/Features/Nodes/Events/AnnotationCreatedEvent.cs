using iPath.Domain.Notificxations;

namespace iPath.Application.Features.Nodes;

public class AnnotationCreatedEvent : NodeEvent, INotification
{
    public NodeEvent Event => this;
}
