using System.ComponentModel.DataAnnotations;

namespace iPath.Domain.Entities;

public class CaseEmbedding : IBaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CaseId { get; set; }
    
    public byte[]? VectorData { get; set; } // We will store sqlite-vec vectors here

    public string? ModelIdentifierUsed { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
