namespace iPath.Application.Features.Documents;

public record VsiImportCommand(string Path, Guid RequestId, Guid? ParentId, bool DeleteAfterImport = false)
    : IRequest<VsiImportCommand, Task<VsiImportResponse>>;
