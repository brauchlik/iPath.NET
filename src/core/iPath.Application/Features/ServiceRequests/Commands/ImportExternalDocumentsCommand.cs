namespace iPath.Application.Features.ServiceRequests;

public record ImportExternalDocumentsCommand(Guid uploadFolderId, IReadOnlyList<string>? storgeIds)
    : IRequest<ImportExternalDocumentsCommand, Task<int>>;
