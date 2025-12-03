using iPath.Domain.Notificxations;

namespace iPath.Application.Features.Nodes;

public class RootNodePublishedEvent : NodeEvent, INotification, IHasNodeNotification
{
    public NodeNofitication ToNotification()
    {
        return this.ToNotif(eNodeEventType.NodePublished, "new node published");
    }
}
