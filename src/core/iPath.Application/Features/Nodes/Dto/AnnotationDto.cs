namespace iPath.Application.Features.Nodes;

public record AnnotationDto
{
    public Guid Id { get; init; }
    public DateTime CreatedOn { get; init; }
    public Guid OwnerId { get; init; }
    public required OwnerDto Owner { get; init; }
    public string? Text { get; init; }
    public Guid? ChildNodeId { get; init; }
    public AnnotationData? Data { get; init; }

    public ICollection<QuestionnaireResponse> QuestionnaireResponses { get; init; } = [];
}
