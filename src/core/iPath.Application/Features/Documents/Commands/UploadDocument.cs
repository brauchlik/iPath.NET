namespace iPath.Application.Features.Documents;


public record UploadDocumentCommand(Guid RequestId, Guid? ParentId, string filename, long fileSize, Stream? fileStream, string? contenttype = null, string? FilePath = null)
    : IRequest<UploadDocumentCommand, Task<DocumentDto>>;



public record UploadDocumentInput(Guid RequestId, Guid? ParentId, string filename) : IEventInput
{
    public string ObjectName => nameof(ServiceRequest);
}
