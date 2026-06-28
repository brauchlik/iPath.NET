using iPath.Application.Features.Documents;
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Documents.Commands;

public class WsiImportCommandHandler(
    IMediator mediator,
    ILogger<WsiImportCommandHandler> logger)
    : IRequestHandler<WsiImportCommand, Task<WsiImportResponse>>
{
    public async Task<WsiImportResponse> Handle(WsiImportCommand request, CancellationToken ct)
    {
        var importedFiles = new List<string>();
        var errors = new List<string>();

        IEnumerable<string> vsiPaths = [];

        if (Directory.Exists(request.Path))
        {
            var files = Directory.GetFiles(request.Path, "*.vsi", SearchOption.TopDirectoryOnly);
            var validPaths = new List<string>();
            foreach (var file in files)
            {
                var baseName = Path.GetFileNameWithoutExtension(file);
                var companion = Path.Combine(Path.GetDirectoryName(file)!, $"_{baseName}_");
                if (Directory.Exists(companion))
                {
                    validPaths.Add(file);
                }
                else
                {
                    errors.Add($"Companion folder '{companion}' not found for VSI slide: {file}");
                    logger.LogWarning("VSI import skipped: Companion folder '{Companion}' not found for '{File}'", companion, file);
                }
            }
            vsiPaths = validPaths;
        }
        else if (File.Exists(request.Path) && Path.GetExtension(request.Path).Equals(".vsi", StringComparison.OrdinalIgnoreCase))
        {
            var baseName = Path.GetFileNameWithoutExtension(request.Path);
            var companion = Path.Combine(Path.GetDirectoryName(request.Path)!, $"_{baseName}_");
            if (Directory.Exists(companion))
            {
                vsiPaths = [request.Path];
            }
            else
            {
                logger.LogWarning("VSI import failed: Companion folder '{Companion}' not found for '{File}'", companion, request.Path);
                return new WsiImportResponse(0, [], [$"Companion folder '{companion}' not found for VSI slide: {request.Path}"]);
            }
        }
        else
        {
            return new WsiImportResponse(0, [], [$"Not a .vsi file or folder: {request.Path}"]);
        }

        foreach (var vsiPath in vsiPaths)
        {
            try
            {
                var fileName = Path.GetFileName(vsiPath);
                var fileInfo = new FileInfo(vsiPath);
                var cmd = new UploadDocumentCommand(
                    RequestId: request.RequestId,
                    ParentId: request.ParentId,
                    filename: fileName,
                    fileSize: fileInfo.Length,
                    fileStream: null,
                    contenttype: null,
                    FilePath: vsiPath);

                var result = await mediator.Send(cmd, ct);
                importedFiles.Add(vsiPath);
                logger.LogInformation("VSI import queued for {Path}", vsiPath);

                if (request.DeleteAfterImport)
                {
                    var baseName = Path.GetFileNameWithoutExtension(vsiPath);
                    var companionDir = Path.Combine(Path.GetDirectoryName(vsiPath)!, baseName);

                    try
                    {
                        File.Delete(vsiPath);
                        if (Directory.Exists(companionDir))
                        {
                            Directory.Delete(companionDir, true);
                            logger.LogInformation("Deleted companion folder {Dir}", companionDir);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{vsiPath} (delete failed): {ex.Message}");
                        logger.LogWarning(ex, "Failed to delete {Path} after import", vsiPath);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{vsiPath}: {ex.Message}");
                logger.LogError(ex, "VSI import failed for {Path}", vsiPath);
            }
        }

        return new WsiImportResponse(
            importedFiles.Count - errors.Count(e => !e.Contains("delete")),
            importedFiles,
            errors);
    }
}
