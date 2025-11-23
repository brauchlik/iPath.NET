using iPath.Domain.Notificxations;

namespace iPath.Application.Features.Nodes;

public class RootNodeCreatedEvent : NodeEvent, INotification, IHasNodeNotification
{
    public NodeNofitication ToNotification()
    {
        return this.ToNotif(eNodeEventType.NewNode, "new node");
    }
}
