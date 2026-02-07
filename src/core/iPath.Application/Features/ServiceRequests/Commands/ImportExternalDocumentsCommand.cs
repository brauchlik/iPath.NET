namespace iPath.Application.Features.ServiceRequests;

public record ImportExternalDocumentsCommand(Guid serviceRequestId, IReadOnlyList<string> storgeIds)
    : IRequest<ImportExternalDocumentsCommand, Task>;
