using System.ComponentModel.DataAnnotations;

namespace iPath.Application.Features.ServiceRequests;

public record AnnotationDto
{
    [Required]
    public Guid Id { get; init; }
    [Required]
    public Guid ServiceRequestId { get; init; }
    public DateTime CreatedOn { get; init; }
    public Guid OwnerId { get; init; }
    public required OwnerDto Owner { get; init; }
    public Guid? DocumentId { get; init; }
    public AnnotationData? Data { get; init; }
}
