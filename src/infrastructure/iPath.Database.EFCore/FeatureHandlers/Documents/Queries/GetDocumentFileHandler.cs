using iPath.Domain.Config;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace iPath.EF.Core.FeatureHandlers.Documents.Queries;


public class GetDocumentFileHandler(iPathDbContext db,
    IRemoteStorageService srvStorage,
    IUserSession sess,
    IOptions<iPathConfig> opts,
    IMemoryCache cache,
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<GetDocumentFileQuery, Task<FetchFileResponse>>
{
    // A slide viewer requests dozens of tiles per second for the same document. The document's
    // group and file info are cached briefly so a tile costs no database round trip; the access
    // check itself still runs on every request (in memory, against the cached session).
    private static readonly TimeSpan MetadataLifetime = TimeSpan.FromSeconds(60);

    private sealed record DocumentFileMetadata(Guid DocumentId, Guid GroupId, Guid RequestId, NodeFile? File);

    public async Task<FetchFileResponse> Handle(GetDocumentFileQuery request, CancellationToken cancellationToken)
    {
        var document = await GetMetadataAsync(request.documentId, cancellationToken);
        if (document is null)
            return new FetchFileResponse(NotFound: true);

        var isGuestAuthorized = httpContextAccessor.HttpContext?.User?.HasClaim("AuthorizedRequestId", document.RequestId.ToString()) == true;

        try
        {
            if (!isGuestAuthorized)
            {
                sess.AssertInGroup(document.GroupId);
            }
        }
        catch (NotAllowedException)
        {
            return new FetchFileResponse(AccessDenied: true);
        }

        var fn = Path.Combine(opts.Value.TempDataPath, document.DocumentId.ToString());

        string? storagePath = null;
        if (document.File?.Storage?.ProviderName == "LocalFiles" && !string.IsNullOrEmpty(document.File.Storage.StorageId))
        {
            var dir = Path.Combine(opts.Value.LocalDataPath, document.GroupId.ToString(), document.RequestId.ToString());
            storagePath = Path.Combine(dir, document.File.Storage.StorageId);
            if (!System.IO.File.Exists(storagePath))
                storagePath = null;
        }

        // A local storage file is served in place; only remote files are fetched into the temp cache.
        if (storagePath is null && !System.IO.File.Exists(fn))
        {
            await srvStorage.GetFileAsync(document.DocumentId, cancellationToken);
        }

        if (storagePath is null && !System.IO.File.Exists(fn))
            return new FetchFileResponse(NotFound: true);

        return new FetchFileResponse(TempFile: fn, Info: document.File, StorageFilePath: storagePath);
    }

    private async Task<DocumentFileMetadata?> GetMetadataAsync(Guid documentId, CancellationToken ct)
    {
        var key = $"document-file-metadata:{documentId}";
        if (cache.TryGetValue(key, out DocumentFileMetadata? cached))
            return cached;

        var document = await db.Documents
            .Include(d => d.ServiceRequest)
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == documentId, ct);
        var metadata = document?.ServiceRequest is null
            ? null
            : new DocumentFileMetadata(document.Id, document.ServiceRequest.GroupId, document.ServiceRequestId, document.File);

        // Only found documents are cached: a document still being uploaded must appear at once.
        if (metadata is not null)
            cache.Set(key, metadata, MetadataLifetime);
        return metadata;
    }
}