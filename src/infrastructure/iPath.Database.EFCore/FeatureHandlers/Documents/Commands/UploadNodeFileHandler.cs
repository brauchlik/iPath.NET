using iPath.Application;
using iPath.Application.Contracts;
using iPath.Application.Features.Documents;
using iPath.Domain.Config;
using iPath.Domain.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Compression;
namespace iPath.EF.Core.FeatureHandlers.Documents.Commands;



public class UploadDocumentFileCommandHandler(iPathDbContext db,
    IOptions<iPathConfig> opts,
    IOptions<iPathClientConfig> clientOpts,
    IUserSession sess,
    IThumbImageService srvThumb,
    IMediator mediator,
    IRemoteStorageUploadQueue queue,
    IWsiConversionQueue wsiConversionQueue,
    IMimetypeService srvMime,
    IOptions<WsiConversionConfig> wsiConfig,
    IEnumerable<IConversionPlugin> conversionPlugins,
    ILogger<UploadDocumentFileCommandHandler> logger)
    : IRequestHandler<UploadDocumentCommand, Task<DocumentDto>>
{
    public async Task<DocumentDto> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        if (!System.IO.Directory.Exists(opts.Value.TempDataPath))
        {
            throw new NotFoundException(opts.Value.TempDataPath, "temp");
        }

        var serviceRequest = await db.ServiceRequests
            .Include(x => x.Documents)
            .AsNoTracking()
            .SingleOrDefaultAsync(n => n.Id == request.RequestId, ct);

        Guard.Against.NotFound(request.RequestId, serviceRequest);

        var document = new DocumentNode
        {
            Id = Guid.CreateVersion7(),
            ServiceRequestId = serviceRequest.Id,
            ParentNodeId = request.ParentId,
            CreatedOn = DateTime.UtcNow,
            OwnerId = sess.User.Id
        };

        document.SortNr = serviceRequest.Documents.IsEmpty() ? 0 : serviceRequest.Documents.Where(n => n.ParentNodeId == request.ParentId).Max(n => n.SortNr) + 1;

        document.File = new()
        {
            Filename = request.filename,
            MimeType = request.contenttype ?? srvMime.GetMimeType(request.filename),
        };

        var ext = Path.GetExtension(request.filename);
        if (document.File.MimeType.ToLower().StartsWith("image"))
        {
            document.DocumentType = "image";
        }
        else if (clientOpts.Value.WsiExtensions.Contains(ext))
        {
            document.DocumentType = "wsi";
        }
        else
        {
            document.DocumentType = "file";
        }

        var plugin = conversionPlugins.FirstOrDefault(p => p.CanHandle(ext));

        using var tran = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var fn = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
            logger.LogInformation("file upload, copy to: " + fn);

            if (!string.IsNullOrEmpty(request.FilePath) && System.IO.File.Exists(request.FilePath))
            {
                System.IO.File.Copy(request.FilePath, fn, true);
            }
            else if (request.fileStream != null)
            {
                using var fileStream = File.Create(fn);
                request.fileStream.Seek(0, SeekOrigin.Begin);
                await request.fileStream.CopyToAsync(fileStream, ct);
            }
            else
            {
                throw new InvalidOperationException("Either FilePath or fileStream must be provided");
            }

            var isZip = string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase);
            var zipPlugin = isZip ? await FindPluginInZipAsync(fn, ct) : null;
            var activePlugin = plugin ?? zipPlugin;

            if (activePlugin != null && activePlugin.RequiresConversion && !wsiConfig.Value.Enabled)
            {
                logger.LogWarning("WSI conversion disabled, saving {File} as regular file", request.filename);
                document.File.ConversionSkipped = true;
                activePlugin = null;
            }

            if (activePlugin != null)
            {
                document.DocumentType = "wsi";
                document.File.ConversionStatus = DocumentConversionStatus.Pending;
            }
            else if (document.File.MimeType.ToLower().StartsWith("image"))
            {
                document.DocumentType = "image";
                await srvThumb.UpdateNodeAsync(document.File, fn);
            }

            await db.Documents.AddAsync(document);
            await db.SaveChangesAsync(ct);
            await tran.CommitAsync(ct);

            if (activePlugin != null)
            {
                var stagingRoot = string.IsNullOrEmpty(wsiConfig.Value.StagingPath)
                    ? Path.Combine(opts.Value.TempDataPath, "conversion")
                    : wsiConfig.Value.StagingPath;
                var stagingDir = Path.Combine(stagingRoot, document.Id.ToString());
                Directory.CreateDirectory(stagingDir);

                // Copy the original file to the staging directory for the conversion worker
                var stagingFilePath = Path.Combine(stagingDir, request.filename);
                System.IO.File.Copy(fn, stagingFilePath, true);

                if (!string.IsNullOrEmpty(request.FilePath))
                {
                    var sourceDir = Path.GetDirectoryName(request.FilePath)!;
                    foreach (var companion in activePlugin.GetRequiredCompanions(request.filename))
                    {
                        var companionSrc = Path.Combine(sourceDir, companion);
                        var companionDst = Path.Combine(stagingDir, companion);
                        if (Directory.Exists(companionSrc))
                            CopyDirectory(companionSrc, companionDst);
                    }
                }

                db.Set<WsiConversionJob>().Add(new WsiConversionJob
                {
                    Id = Guid.CreateVersion7(),
                    DocumentId = document.Id,
                    OriginalStorageId = stagingDir,
                    PluginType = activePlugin.GetType().Name
                });
                await db.SaveChangesAsync(ct);
                await wsiConversionQueue.EnqueueAsync(document.Id, ct);

                logger.LogInformation("VSI conversion enqueued for document {DocId}", document.Id);
            }
            else
            {
                // Only enqueue remote storage immediately if no conversion plugin is handling it
                await queue.EnqueueAsync(new RemoteStorageCommand(document.Id, eRemoteStorageCommand.UploadDocument), ct);
            }

            var dto = document.ToDto();
            return dto;
        }
        catch (Exception ex)
        {
            await tran.RollbackAsync(ct);
            var msg = ex.InnerException is null ? ex.Message : ex.InnerException.Message;
            Console.WriteLine(msg);
            throw;
        }
    }

    private async Task<IConversionPlugin?> FindPluginInZipAsync(string zipPath, CancellationToken ct)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var plugin in conversionPlugins)
            {
                if (plugin.CanHandleZip(archive))
                    return plugin;
            }

            // no plugin handles this zip — caller imports as generic file
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read ZIP file {Path}", zipPath);
        }
        return null;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;
        Directory.CreateDirectory(destinationDir);
        foreach (var file in dir.GetFiles())
            file.CopyTo(Path.Combine(destinationDir, file.Name), true);
        foreach (var subDir in dir.GetDirectories())
            CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name));
    }
}
