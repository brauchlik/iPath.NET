using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Commands;




public class UploadNodeFileCommandHandler(iPathDbContext db,
    IOptions<iPathConfig> opts,
    IUserSession sess,
    IThumbImageService srvThumb,
    IMediator mediator,
    IUploadQueue queue,
    ILogger<UploadNodeFileCommandHandler> logger)
    : IRequestHandler<UploadNodeFileCommand, Task<NodeListDto>>
{
    public async Task<NodeListDto> Handle(UploadNodeFileCommand request, CancellationToken ct)
    {
        if (!System.IO.Directory.Exists(opts.Value.TempDataPath))
        {
            throw new NotFoundException(opts.Value.TempDataPath, "temp");
        }

        // get root node
        var rootNode = await db.Nodes.Include(n => n.ChildNodes).FirstOrDefaultAsync(n => n.Id == request.RootNodeId, ct);
        Guard.Against.NotFound(request.RootNodeId, rootNode);

        // validate parent
        if (request.RootNodeId != request.ParentNodeId)
        {
            var parentNode = rootNode.ChildNodes.FirstOrDefault(n => n.Id == request.ParentNodeId);
            Guard.Against.NotFound(request.ParentNodeId, parentNode);
        }

        // create entity
        var newNode = new Node
        {
            RootNodeId = request.RootNodeId,
            ParentNodeId = request.ParentNodeId,
            CreatedOn = DateTime.UtcNow,
            OwnerId = sess.User.Id
        };

        newNode.SortNr = rootNode.ChildNodes.Where(n => n.ParentNodeId == request.ParentNodeId).Max(n => n.SortNr) + 1;
        newNode.SortNr ??= 0;

        newNode.File = new()
        {
            Filename = request.filename,
            MimeType = GetMimeType(request.filename),
        };

        // node type
        newNode.NodeType = newNode.File.MimeType.ToLower().StartsWith("image") ? "image" : "file";

        using var tran = await db.Database.BeginTransactionAsync(ct);

        // save node to generate ID
        try
        {
            await db.Nodes.AddAsync(newNode);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException is null ? ex.Message : ex.InnerException.Message;
            Console.WriteLine(msg);
            await tran.RollbackAsync();
            throw ex;
        }

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
            await db.SaveChangesAsync();
        }

        // publish domain event
        var evtinput = new UploadNodeFileInput(ParentNodeId: request.ParentNodeId, RootNodeId: request.RootNodeId, filename: request.filename);
        var evt = await db.CreateEventAsync<ChildNodeCreatedEvent, UploadNodeFileInput>(evtinput, newNode.Id, sess.User.Id);
        evt.RootParentId = newNode.RootNodeId;

        await db.SaveChangesAsync(ct);
        await tran.CommitAsync(ct);

        // copy to storage
        await queue.EnqueueAsync(newNode.Id);

        // publish domain event
        await mediator.Publish(evt, ct);

        // return dto
        return newNode.ToListDto();
    }



    private static readonly List<string> ImageExtensions = new List<string> { ".JPG", ".JPEG", ".JPE", ".BMP", ".GIF", ".PNG" };

    private bool IsImage(string Filename)
    {
        try
        {
            var fi = new FileInfo(Filename);
            return ImageExtensions.Contains(fi.Extension.ToUpper());
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
        }
        return false;
    }

    private string GetMimeType(string Filename)
    {
        if (MimeTypes.TryGetMimeType(Filename, out var mimeType))
        {
            return mimeType;
        }
        return "application/octet-stream";
    }

}
