using System.Text.Json.Serialization;

namespace iPath.Application.Features.Nodes;


public class NodeEvent : EventEntity
{
    [JsonIgnore]
    public Node? Node { get; set; }

}
