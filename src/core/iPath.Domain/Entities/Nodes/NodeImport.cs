namespace iPath.Domain.Entities;

public class NodeImport : BaseEntity
{
    public Guid NodeId { get; set; }

    public string? Data { get; set; }
    public string? Info { get; set; }
}