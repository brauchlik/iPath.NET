namespace iPath.Application.Features.Nodes;

public record AnnotationDto
{
    public Guid Id { get; init; }
    public DateTime CreatedOn { get; init; }
    public Guid OwnerId { get; init; }
    public required OwnerDto Owner { get; init; }
    public string? Text { get; init; }

    public ICollection<QuestionnaireResponse> QuestionnaireResponses { get; init; } = [];
}


public static class AnnotationExtensions
{
    public static AnnotationDto ToDto(this Annotation item)
    {
        return new AnnotationDto
        {
            Id = item.Id,
            CreatedOn = item.CreatedOn,
            OwnerId = item.OwnerId,
            Owner = item.Owner.ToOwnerDto(),
            Text = item.Text,
            QuestionnaireResponses = item.QuestionnaireResponses
        };
    }
}