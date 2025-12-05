namespace iPath.Domain.Entities;

/*
public interface INodeEvent
{
    Node Node { get; }
}
*/


public class NodeEvent : EventEntity //, INodeEvent
{
    [JsonIgnore]
    public Node Node { get; set; }
}

