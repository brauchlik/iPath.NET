namespace iPath.Application.Features.Documents;

public record VsiImportRequest(string Path, Guid RequestId, Guid? ParentId, bool DeleteAfterImport = false);

public record VsiImportResponse(int Imported, List<string> ImportedFiles, List<string> Errors);
