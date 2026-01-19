namespace iPath.Application.Features.ServiceRequests;

public record AnnotationDto
{
    public Guid Id { get; init; }
    public DateTime CreatedOn { get; init; }
    public Guid OwnerId { get; init; }
    public required OwnerDto Owner { get; init; }
    public Guid? DocumentId { get; init; }
    public AnnotationData? Data { get; init; }
}
