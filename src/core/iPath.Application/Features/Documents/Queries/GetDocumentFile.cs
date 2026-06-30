namespace iPath.Application.Features;

public record GetDocumentFileQuery(Guid documentId) 
    : IRequest<GetDocumentFileQuery, Task<FetchFileResponse>>;


public record FetchFileResponse(string TempFile = "", NodeFile? Info = null, bool NotFound = false, bool AccessDenied = false, string? StorageFilePath = null);

