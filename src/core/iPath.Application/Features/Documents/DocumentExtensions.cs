namespace iPath.Application.Features.Documents;

public static class DocumentExtensions
{
    public static DocumentDto ToDto(this DocumentNode document)
    {
        return new DocumentDto
        {
            Id = document.Id,
            CreatedOn = document.CreatedOn,
            Deleted = document.DeletedOn.HasValue, 
            SortNr = document.SortNr,
            DocumentType = document.DocumentType,
            OwnerId = document.OwnerId,
            Owner = document.Owner.ToOwnerDto(),
            ServiceRequestId = document.ServiceRequestId,
            ParentNodeId = document.ParentNodeId,
            // A copy: in Server mode the DTO reaches the UI in-process, and UI edits to the tracked
            // entity's file would be written back by the next save of that DbContext.
            File = document.File?.Clone(),
            ipath2_id = document.ipath2_id
        };
    }

    public static bool IsSameAs(this DocumentNode? doc, DocumentNode? other)
    {
        return doc is not null && other is not null && doc.Id == other.Id;
    }
}