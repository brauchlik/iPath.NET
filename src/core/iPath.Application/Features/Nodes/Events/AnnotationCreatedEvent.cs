using iPath.Domain.Notificxations;

namespace iPath.Application.Features.Nodes;

public class AnnotationCreatedEvent : NodeEvent, INotification, IHasNodeNotification
{
    public NodeNofitication ToNotification()
    {
        return this.ToNotif(eNodeEventType.NewAnnotation, "new annotation");
    }
}
