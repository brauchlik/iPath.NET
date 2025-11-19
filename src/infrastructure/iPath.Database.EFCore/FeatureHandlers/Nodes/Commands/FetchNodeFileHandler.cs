using iPath.Domain.Config;
using Microsoft.Extensions.Options;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;


public class FetchNodeFileHandler(iPathDbContext db,
    IStorageService srvStorage, 
    IUserSession sess,
    IOptions<iPathConfig> opts)
    : IRequestHandler<GetNodeFileQuery, Task<FetchFileResponse>>
{
    public async Task<FetchFileResponse> Handle(GetNodeFileQuery request, CancellationToken cancellationToken)
    {
        var node = await db.Nodes
                   .Include(n => n.File)
                   .Include(n => n.RootNode)
                   .AsNoTracking()
                   .FirstOrDefaultAsync(n => n.Id == request.nodeId);

        if (node is null || node.File is null || node.RootNode is null)
            return new FetchFileResponse(NotFound: true);

        // TODO: implement authentication 
        //if (!srvUser.CurrentUser.AllowedGroupIds().Contains(node.RootNode.GroupId.Value))
        //    return new FetchFileResponse(AccessDenied: true);

        var fn = Path.Combine(opts.Value.TempDataPath, node.Id.ToString());

        // get file form store if no local copy exists
        if (!System.IO.File.Exists(fn))
        {
            await srvStorage.GetNodeFileAsync(node, cancellationToken);
        }

        if (!System.IO.File.Exists(fn))
            return new FetchFileResponse(NotFound: true);

        return new FetchFileResponse(TempFile: fn, Info: node.File);
    }
}