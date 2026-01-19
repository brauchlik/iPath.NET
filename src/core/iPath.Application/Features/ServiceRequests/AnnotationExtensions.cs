namespace iPath.Application.Features.ServiceRequests;

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
            Data = item.Data,
            DocumentId = item.DcoumentNodeId
        };
    }

    extension (AnnotationData Data)
    {

        public bool ValidateInput()
        {
            if (!string.IsNullOrWhiteSpace(Data.Text)) return true;
            if (Data.Morphology is not null)
            {
                Data.Text ??= Data.Morphology.Display; // Morphology as default text if no text written
                return true;
            }
            if (Data.Questionnaire is not null) return true;
            return false;
        }
    }
}