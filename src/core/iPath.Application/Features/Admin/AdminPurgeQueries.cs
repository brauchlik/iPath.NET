namespace iPath.Application.Features.Admin;

public record GetDeletedDocumentsWithFilesQuery()
    : IRequest<GetDeletedDocumentsWithFilesQuery, Task<List<PurgeDocumentFileDto>>>;

public record PurgeDocumentFilesCommand(Guid DocumentId)
    : IRequest<PurgeDocumentFilesCommand, Task<bool>>;

public record GetStaleCacheFilesQuery(int DaysOld = 7)
    : IRequest<GetStaleCacheFilesQuery, Task<List<StaleCacheFileDto>>>;

public record CleanStaleCacheFilesCommand(int DaysOld = 7)
    : IRequest<CleanStaleCacheFilesCommand, Task<int>>;
