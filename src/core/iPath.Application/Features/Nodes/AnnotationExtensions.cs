namespace iPath.Application.Features.Nodes;

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
            Data = item.Data,
            ChildNodeId = item.ChildNodeId,
            QuestionnaireResponses = item.QuestionnaireResponses
        };
    }
}