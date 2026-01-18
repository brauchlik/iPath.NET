namespace iPath.Application.Features.Documents;

public static class DocumentExtensions
{
    public static DocumentDto ToDto(this DocumentNode document)
    {
        return new DocumentDto
        {
            Id = document.Id,
            CreatedOn = document.CreatedOn,
            SortNr = document.SortNr,
            DocumentType = document.DocumentType,
            OwnerId = document.OwnerId,
            Owner = document.Owner.ToOwnerDto(),
            ServiceRequestId = document.ServiceRequestId,
            ParentNodeId = document.ParentNodeId,
            File = document.File,
            ipath2_id = document.ipath2_id
        };
    }

    public static bool IsSameAs(this DocumentNode? doc, DocumentNode? other)
    {
        return doc is not null && other is not null && doc.Id == other.Id;
    }


}