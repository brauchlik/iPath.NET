namespace iPath.Application.Features;

public record GetDocumentFileQuery(Guid documentId) 
    : IRequest<GetDocumentFileQuery, Task<FetchFileResponse>>;


public record FetchFileResponse(string TempFile = "", NodeFile? Info = null, bool NotFound = false, bool AccessDenied = false, string? StorageFilePath = null)
{
    /// <summary>The local file to serve: the local storage file when there is one, else the temp copy.</summary>
    public string? ServePath =>
        !string.IsNullOrEmpty(StorageFilePath) && File.Exists(StorageFilePath) ? StorageFilePath
        : !string.IsNullOrEmpty(TempFile) && File.Exists(TempFile) ? TempFile
        : null;
}

