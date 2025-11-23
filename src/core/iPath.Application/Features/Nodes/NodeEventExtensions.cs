using DispatchR.Abstractions.Send;
using iPath.Domain.Notificxations;
using System.Text.Json;

namespace iPath.Application.Features.Nodes;

public static class NodeEventExtensions
{
    internal static EventEntity CreateEvent<TEvent, TInput>(this Node node,
        TInput input,
        Guid? userId = null,
        CancellationToken ct = default)
        where TEvent : NodeEvent, new()
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
            Node = node,
        };
        node.AddEventEntity(e);
        return e;
    }
}

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


    internal static NodeNofitication ToNotif(this NodeEvent e, eNodeEventType t, string msg)
    {
        return new NodeNofitication(
            NodeId: e.ObjectId,
            UserId: e.UserId,
            OwnerId: e.Node?.OwnerId,
            GroupId: e.Node?.GroupId,
            EventDate: DateTime.UtcNow,
            type: t, 
            msg);
    }


}
