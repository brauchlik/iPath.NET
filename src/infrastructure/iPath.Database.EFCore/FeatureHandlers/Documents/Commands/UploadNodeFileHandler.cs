using iPath.Application;
using iPath.Application.Features.Documents;
using iPath.Domain.Config;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.EF.Core.FeatureHandlers.Documents.Commands;




public class UploadDocumentFileCommandHandler(iPathDbContext db,
    IOptions<iPathConfig> opts,
    IUserSession sess,
    IThumbImageService srvThumb,
    IMediator mediator,
    IUploadQueue queue,
    IMimetypeService srvMime,
    ILogger<UploadDocumentFileCommandHandler> logger)
    : IRequestHandler<UploadDocumentCommand, Task<DocumentDto>>
{
    public async Task<DocumentDto> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        if (!System.IO.Directory.Exists(opts.Value.TempDataPath))
        {
            throw new NotFoundException(opts.Value.TempDataPath, "temp");
        }

        // get root node
        var node = await db.ServiceRequests
            .Include(x => x.Documents)
            .AsNoTracking()
            .SingleOrDefaultAsync(n => n.Id == request.RequestId, ct);

        Guard.Against.NotFound(request.RequestId, node);

        // create entity
        var document = new DocumentNode
        {
            Id = Guid.CreateVersion7(),
            ServiceRequestId = node.Id,
            ParentNodeId = request.ParentId,
            CreatedOn = DateTime.UtcNow,
            OwnerId = sess.User.Id
        };

        // rootNode.ChildNodes.Add(newNode);

        document.SortNr = node.Documents.IsEmpty() ? 0 : node.Documents.Where(n => n.ParentNodeId == request.ParentId).Max(n => n.SortNr) + 1;

        document.File = new()
        {
            Filename = request.filename,
            MimeType = request.contenttype ?? MimeTypes.GetMimeType(request.filename),
        };

        // node type
        document.DocumentType = document.File.MimeType.ToLower().StartsWith("image") ? "image" : "file";

        using var tran = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Save the file to local temp folder
            var fn = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
            logger.LogInformation("file upload, copy to: " + fn);

            using (var fileStream = File.Create(fn))
            {
                request.fileStream.Seek(0, SeekOrigin.Begin);
                await request.fileStream.CopyToAsync(fileStream, ct);
            }

            // generate thumbnail
            if (document.File.MimeType.ToLower().StartsWith("image"))
            {
                document.DocumentType = "image";
                await srvThumb.UpdateNodeAsync(document.File, fn);
            }

            // insert the newNode into the DB
            await db.Documents.AddAsync(document);

            // publish domain event
            var evtinput = new UploadDocumentInput(RequestId: node.Id, ParentId: request.ParentId, filename: request.filename);
            // TODO: event

            await db.SaveChangesAsync(ct);
            await tran.CommitAsync(ct);

            // copy to storage
            await queue.EnqueueAsync(document.Id);

            // return dto
            var dto = document.ToDto();
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
