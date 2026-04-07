using System.Text.Json;

namespace iPath.Application.Features.ServiceRequests;

public static class ServiceRequestEventExtensions
{
    public static EventEntity CreateEvent<TEvent, TInput>(this ServiceRequest node,
        TInput input,
        Guid userId,
        CancellationToken ct = default)
        where TEvent : ServiceRequestEvent, new()
        where TInput : IEventInput
    {
        var e = new TEvent
        {
            EventId = Guid.CreateVersion7(),
            EventDate = DateTime.UtcNow,
            UserId = userId,
            EventName = typeof(TEvent).Name,
            ObjectName = input.ObjectName,
            ObjectId = node.Id,
            Payload = JsonSerializer.Serialize(input),
            ServiceRequest = node,
        };
        node.LastModifiedOn = DateTime.UtcNow;
        node.AddEventEntity(e);
        return e;
    }

    /*
    internal static NodeNofitication ToNotif(this NodeEvent e, eNodeEventType t)
    {
        return new NodeNofitication(
            Event: e,
            type: t);
    }
    */
}

/*
public static class CreateNodeExtensions
{
    public static Node CreateNode(CreateNodeCommand cmd, Guid userId)
    {
        var node = new Node
        {
            Id = cmd.NodeId.HasValue ? cmd.NodeId.Value : Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
            GroupId = cmd.GroupId,
            OwnerId = userId,
            Description = cmd.Description ?? new(),
            NodeType = cmd.NodeType,
            IsDraft = true
        };

        node.CreateEvent<RootNodeCreatedEvent, CreateNodeCommand>(cmd, userId);
        return node;
    }

}
*/
