namespace iPath.Application.Features;

/// <param name="FetchRemote">
/// Fetch a remote file into the temp cache when no local copy exists. False for callers that
/// range-read the instance directly (tiles, partial downloads).
/// </param>
public record GetDocumentFileQuery(Guid documentId, bool FetchRemote = true)
    : IRequest<GetDocumentFileQuery, Task<FetchFileResponse>>;


public record FetchFileResponse(string TempFile = "", NodeFile? Info = null, bool NotFound = false, bool AccessDenied = false, string? StorageFilePath = null,
    string? StorageInstance = null, string? StorageKey = null)
{
    /// <summary>The local file to serve: the local storage file when there is one, else the temp copy.</summary>
    public string? ServePath =>
        !string.IsNullOrEmpty(StorageFilePath) && File.Exists(StorageFilePath) ? StorageFilePath
        : !string.IsNullOrEmpty(TempFile) && File.Exists(TempFile) ? TempFile
        : null;
}

