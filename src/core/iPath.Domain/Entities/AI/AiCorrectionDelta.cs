using System.ComponentModel.DataAnnotations;

namespace iPath.Domain.Entities;

public class AiCorrectionDelta : IBaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? GroupId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? WrongPrediction { get; set; }
    public string? CorrectedValue { get; set; }
    public string? ContextualSnippet { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
