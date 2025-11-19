namespace iPath.Application.Features;

public record GetNodeFileQuery(Guid nodeId) 
    : IRequest<GetNodeFileQuery, Task<FetchFileResponse>>;


public record FetchFileResponse(string TempFile = "", NodeFile? Info = null, bool NotFound = false, bool AccessDenied = false);

