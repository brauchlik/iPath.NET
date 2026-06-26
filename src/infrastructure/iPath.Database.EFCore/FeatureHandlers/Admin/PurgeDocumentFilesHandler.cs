using iPath.Application.Contracts;
using iPath.Application.Features.Admin;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DispatchR.Abstractions.Send;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class PurgeDocumentFilesHandler(
    IOptions<iPathConfig> ipathOpts,
    IOptions<VsiConversionConfig> vsiOpts,
    IRemoteStorageService storage,
    ILogger<PurgeDocumentFilesHandler> logger)
    : IRequestHandler<PurgeDocumentFilesCommand, Task<bool>>
{
    private readonly string _tempPath = ipathOpts.Value.TempDataPath;
    private readonly string _stagingPath = vsiOpts.Value.StagingPath;

    public async Task<bool> Handle(PurgeDocumentFilesCommand request, CancellationToken ct)
    {
        var id = request.DocumentId.ToString();
        var errors = new List<string>();

        var tempFile = Path.Combine(_tempPath, id);
        try { if (File.Exists(tempFile)) { File.Delete(tempFile); logger.LogInformation("Deleted temp file {Path}", tempFile); } }
        catch (Exception ex) { errors.Add($"temp file: {ex.Message}"); }

        var dziFolder = Path.Combine(_tempPath, $"{id}_files");
        try { if (Directory.Exists(dziFolder)) { Directory.Delete(dziFolder, true); logger.LogInformation("Deleted DZI folder {Path}", dziFolder); } }
        catch (Exception ex) { errors.Add($"DZI folder: {ex.Message}"); }

        if (!string.IsNullOrEmpty(_stagingPath))
        {
            var stagingDir = Path.Combine(_stagingPath, id);
            try { if (Directory.Exists(stagingDir)) { Directory.Delete(stagingDir, true); logger.LogInformation("Deleted staging dir {Path}", stagingDir); } }
            catch (Exception ex) { errors.Add($"staging dir: {ex.Message}"); }
        }

        try { await storage.DeleteFileAsync(request.DocumentId, ct); }
        catch (Exception ex) { errors.Add($"remote storage: {ex.Message}"); }

        if (errors.Count > 0)
        {
            logger.LogWarning("Purge for document {DocId} completed with errors: {Errors}", request.DocumentId, string.Join("; ", errors));
            return false;
        }
        return true;
    }
}
