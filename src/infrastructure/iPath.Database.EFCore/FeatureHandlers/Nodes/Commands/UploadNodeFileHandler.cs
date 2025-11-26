using iPath.Domain.Config;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;




public class UploadNodeFileCommandHandler(iPathDbContext db,
    IOptions<iPathConfig> opts,
    IUserSession sess,
    IThumbImageService srvThumb,
    IMediator mediator,
    IUploadQueue queue,
    IMimetypeService srvMime,
    ILogger<UploadNodeFileCommandHandler> logger)
    : IRequestHandler<UploadNodeFileCommand, Task<NodeDto>>
{
    public async Task<NodeDto> Handle(UploadNodeFileCommand request, CancellationToken ct)
    {
        if (!System.IO.Directory.Exists(opts.Value.TempDataPath))
        {
            throw new NotFoundException(opts.Value.TempDataPath, "temp");
        }

        // get root node
        var rootNodeId = await db.Nodes.AsNoTracking()
            .Where(n => n.Id == request.ParentNodeId)
            .Select(n => n.RootNodeId)
            .SingleOrDefaultAsync(ct);

        rootNodeId ??= request.ParentNodeId; // the specified parent is already the root
        var rootNode = await db.Nodes.Include(n => n.ChildNodes).FirstOrDefaultAsync(n => n.Id == rootNodeId.Value, ct);
        Guard.Against.NotFound(request.ParentNodeId, rootNode);

        // create entity
        var newNode = new Node
        {
            Id = Guid.CreateVersion7(),
            RootNode = rootNode,
            ParentNodeId = request.ParentNodeId,
            CreatedOn = DateTime.UtcNow,
            Owner = await db.Users.FindAsync(sess.User.Id, ct)
        };

        // rootNode.ChildNodes.Add(newNode);

        newNode.SortNr = rootNode.ChildNodes.Where(n => n.ParentNodeId == request.ParentNodeId).Max(n => n.SortNr) + 1;
        newNode.SortNr ??= 0;

        newNode.File = new()
        {
            Filename = request.filename,
            MimeType = request.contenttype ?? MimeTypes.GetMimeType(request.filename),
        };

        // node type
        newNode.NodeType = newNode.File.MimeType.ToLower().StartsWith("image") ? "image" : "file";

        using var tran = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Save the file to local temp folder
            var fn = Path.Combine(opts.Value.TempDataPath, newNode.Id.ToString());
            logger.LogInformation("file upload, copy to: " + fn);

            using (var fileStream = File.Create(fn))
            {
                request.fileStream.Seek(0, SeekOrigin.Begin);
                await request.fileStream.CopyToAsync(fileStream, ct);
            }

            // generate thumbnail
            if (newNode.File.MimeType.ToLower().StartsWith("image"))
            {
                newNode.NodeType = "image";
                await srvThumb.UpdateNodeAsync(newNode.File, fn);
            }

            // insert the newNode into the DB
            newNode.IsDraft = false;
            await db.Nodes.AddAsync(newNode);

            // publish domain event
            var evtinput = new UploadNodeFileInput(ParentNodeId: request.ParentNodeId, RootNodeId: rootNode.Id, filename: request.filename);
            newNode.CreateEvent<ChildNodeCreatedEvent, UploadNodeFileInput>(evtinput, sess.User.Id);

            await db.SaveChangesAsync(ct);
            await tran.CommitAsync(ct);

            // copy to storage
            await queue.EnqueueAsync(newNode.Id);

            // return dto
            var dto = newNode.ToDto();
            return dto;
        }
        catch (Exception ex)
        {
            await tran.RollbackAsync(ct);
            var msg = ex.InnerException is null ? ex.Message : ex.InnerException.Message;
            Console.WriteLine(msg);
            await tran.RollbackAsync();
            throw ex;
        }
    }



}
