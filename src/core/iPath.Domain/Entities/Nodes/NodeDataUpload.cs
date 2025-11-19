namespace iPath.Domain.Entities;

public class NodeDataUpload
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public DateTime ScheduledDate { get; set; }
}