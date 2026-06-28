namespace iPath.Application.Features.Documents;

public record WsiImportCommand(string Path, Guid RequestId, Guid? ParentId, bool DeleteAfterImport = false)
    : IRequest<WsiImportCommand, Task<WsiImportResponse>>;
