namespace iPath.Domain.Entities;


public class NodeLastVisit : IEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    public Guid NodeId { get; set; }
    public Node Node { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    private NodeLastVisit()
    {
    }

    public static NodeLastVisit Create(Guid userId, Guid nodeId, DateTime date)
    {
        return new NodeLastVisit { UserId = userId, NodeId = nodeId, Date = date };
    }
}
