using iPath.Application;
using iPath.Application.Contracts;
using iPath.Application.Features.Documents;
using iPath.Domain.Config;
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
    IVsiConversionQueue vsiConversionQueue,
    IMimetypeService srvMime,
    IOptions<VsiConversionConfig> vsiConfig,
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
            string fn;

            if (plugin != null)
            {
                var stagingRoot = string.IsNullOrEmpty(vsiConfig.Value.StagingPath)
                    ? Path.Combine(opts.Value.TempDataPath, "conversion")
                    : vsiConfig.Value.StagingPath;
                var stagingDir = Path.Combine(stagingRoot, document.Id.ToString());
                Directory.CreateDirectory(stagingDir);
                fn = Path.Combine(stagingDir, request.filename);
                logger.LogInformation("conversion staging: {Path}", fn);

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

                document.DocumentType = "wsi";
            }
            else
            {
                if (!string.IsNullOrEmpty(request.FilePath) && System.IO.File.Exists(request.FilePath))
                {
                    fn = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
                    logger.LogInformation("file upload, copy from: " + request.FilePath + " to: " + fn);
                    System.IO.File.Copy(request.FilePath, fn, true);
                }
                else
                {
                    fn = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
                    logger.LogInformation("file upload, copy to: " + fn);

                    if (request.fileStream == null)
                    {
                        throw new InvalidOperationException("Either FilePath or fileStream must be provided");
                    }

                    using (var fileStream = File.Create(fn))
                    {
                        request.fileStream.Seek(0, SeekOrigin.Begin);
                        await request.fileStream.CopyToAsync(fileStream, ct);
                    }
                }
            }

            if (plugin == null && document.File.MimeType.ToLower().StartsWith("image"))
            {
                document.DocumentType = "image";
                await srvThumb.UpdateNodeAsync(document.File, fn);
            }

            await db.Documents.AddAsync(document);

            var evtinput = new UploadDocumentInput(RequestId: serviceRequest.Id, ParentId: request.ParentId, filename: request.filename);

            await db.SaveChangesAsync(ct);
            await tran.CommitAsync(ct);

            if (plugin != null)
            {
                var stagingRoot = string.IsNullOrEmpty(vsiConfig.Value.StagingPath)
                    ? Path.Combine(opts.Value.TempDataPath, "conversion")
                    : vsiConfig.Value.StagingPath;
                var stagingDir = Path.Combine(stagingRoot, document.Id.ToString());

                if (!string.IsNullOrEmpty(request.FilePath))
                {
                    var sourceDir = Path.GetDirectoryName(request.FilePath)!;
                    foreach (var companion in plugin.GetRequiredCompanions(request.filename))
                    {
                        var companionSrc = Path.Combine(sourceDir, companion);
                        var companionDst = Path.Combine(stagingDir, companion);
                        if (Directory.Exists(companionSrc))
                            CopyDirectory(companionSrc, companionDst);
                    }
                }

                db.Set<VsiConversionJob>().Add(new VsiConversionJob
                {
                    Id = Guid.CreateVersion7(),
                    DocumentId = document.Id,
                    OriginalStorageId = stagingDir
                });
                await db.SaveChangesAsync(ct);
                await vsiConversionQueue.EnqueueAsync(document.Id, ct);
            }
            else if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                // Check if zip contains files handled by a conversion plugin
                var tempZipPath = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
                var zipPlugin = await FindPluginInZipAsync(tempZipPath, ct);
                if (zipPlugin != null)
                {
                    var stagingRoot = string.IsNullOrEmpty(vsiConfig.Value.StagingPath)
                        ? Path.Combine(opts.Value.TempDataPath, "conversion")
                        : vsiConfig.Value.StagingPath;
                    var stagingDir = Path.Combine(stagingRoot, document.Id.ToString());

                    // Extract all zip contents to staging dir
                    if (Directory.Exists(stagingDir))
                        Directory.Delete(stagingDir, true);
                    ZipFile.ExtractToDirectory(tempZipPath, stagingDir);

                    // Remove temp zip file
                    try { File.Delete(tempZipPath); } catch { }

                    document.DocumentType = "wsi";
                    db.Documents.Update(document);

                    db.Set<VsiConversionJob>().Add(new VsiConversionJob
                    {
                        Id = Guid.CreateVersion7(),
                        DocumentId = document.Id,
                        OriginalStorageId = stagingDir
                    });
                    await db.SaveChangesAsync(ct);
                    await vsiConversionQueue.EnqueueAsync(document.Id, ct);

                    logger.LogInformation("ZIP extracted to staging dir {Dir}, VSI conversion enqueued", stagingDir);
                }
            }

            await queue.EnqueueAsync(new RemoteStorageCommand(document.Id, eRemoteStorageCommand.UploadDocument), ct);

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
            foreach (var entry in archive.Entries)
            {
                var entryExt = Path.GetExtension(entry.Name);
                if (!string.IsNullOrEmpty(entryExt))
                {
                    var found = conversionPlugins.FirstOrDefault(p => p.CanHandle(entryExt));
                    if (found != null)
                        return found;
                }
            }
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
