using System.ComponentModel.DataAnnotations;

namespace iPath.Domain.Entities;

public class CaseIngestionLineage : IBaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CaseId { get; set; }
    public Guid? GroupId { get; set; }
    
    [Required]
    public string RawInputText { get; set; } = string.Empty;
    
    [Required]
    public string AiSuggestedDataJson { get; set; } = string.Empty;
    public string? HumanAcceptedDataJson { get; set; }
    
    public string? ModelIdentifierUsed { get; set; }
    public bool WasOverridden { get; set; }
    public bool HasBeenAnalyzedBySupervisor { get; set; } = false;

    public string Status { get; set; } = "Queued";
    public string? ErrorMessage { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
