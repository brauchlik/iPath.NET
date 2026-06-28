namespace iPath.Application.Features.Documents;

public record WsiImportRequest(string Path, Guid RequestId, Guid? ParentId, bool DeleteAfterImport = false);

public record WsiImportResponse(int Imported, List<string> ImportedFiles, List<string> Errors);
