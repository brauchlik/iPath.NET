using System.ComponentModel.DataAnnotations;

namespace iPath.Domain.Entities;

public class ServiceRequestAnswer : IBaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    public string QuestionnaireId { get; set; } = string.Empty;
    public int? QuestionnaireVersion { get; set; }

    public string LinkId { get; set; } = string.Empty;

    public string? CodeSystem { get; set; }
    public string? Code { get; set; }
    public string? CodeDisplay { get; set; }
    public string? OtherCodings { get; set; }

    public string ValueType { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? ValueDisplay { get; set; }
    public string? Unit { get; set; }

    public int ExtractionVersion { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
