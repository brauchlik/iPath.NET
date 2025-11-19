namespace iPath.Application.Features.Nodes;


public class ChildNodeCreatedEvent : NodeEvent, INotification
{
    public Guid? RootParentId { get; set; }
}


