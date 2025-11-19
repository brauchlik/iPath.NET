namespace iPath.Application.Features.Nodes;

public class RootNodeCreatedEvent : NodeEvent, INotification
{
    public Guid? GroupId { get; set; }
}
