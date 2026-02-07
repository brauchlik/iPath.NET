namespace iPath.Application.Features.ServiceRequests;

public record ScanExternalDocumentsQuery(Guid serviceRequestId) 
    : IRequest<ScanExternalDocumentsQuery, Task<ScanExternalDocumentResponse>>;


public record ScanExternalDocumentResponse(string storageName, IEnumerable<ExternalFile>? files);


public record ExternalFile(string StorageId, string Filename, string Mimetype, long? FileSize, DateTimeOffset? CreatedOn);