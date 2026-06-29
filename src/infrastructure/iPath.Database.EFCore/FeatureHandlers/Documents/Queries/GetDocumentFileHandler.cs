using iPath.Domain.Config;
using Microsoft.Extensions.Options;

namespace iPath.EF.Core.FeatureHandlers.Documents.Queries;


public class GetDocumentFileHandler(iPathDbContext db,
    IRemoteStorageService srvStorage, 
    IUserSession sess,
    IOptions<iPathConfig> opts,
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<GetDocumentFileQuery, Task<FetchFileResponse>>
{
    public async Task<FetchFileResponse> Handle(GetDocumentFileQuery request, CancellationToken cancellationToken)
    {
        var document = await db.Documents
                    .Include(d => d.ServiceRequest)
                   .AsNoTracking()
                   .SingleOrDefaultAsync(n => n.Id == request.documentId);

        Guard.Against.NotFound(request.documentId, document);

        var isGuestAuthorized = httpContextAccessor.HttpContext?.User?.HasClaim("AuthorizedRequestId", document.ServiceRequest.Id.ToString()) == true;

        // TODO: implement authentication 
        try
        {
            if (!isGuestAuthorized)
            {
                sess.AssertInGroup(document.ServiceRequest.GroupId);
            }
        }
        catch (NotAllowedException ex)
        {
            return new FetchFileResponse(AccessDenied: true);
        }

        var fn = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());

        // get file form store if no local copy exists
        if (!System.IO.File.Exists(fn))
        {
            await srvStorage.GetFileAsync(document.Id, cancellationToken);
        }

        if (!System.IO.File.Exists(fn))
            return new FetchFileResponse(NotFound: true);

        return new FetchFileResponse(TempFile: fn, Info: document.File);
    }
}