using iPath.Application.Features.Admin;
using iPath.Domain.Config;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DispatchR.Abstractions.Send;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class CleanStaleCacheFilesHandler(
    iPathDbContext db,
    IOptions<iPathConfig> ipathOpts,
    ILogger<CleanStaleCacheFilesHandler> logger)
    : IRequestHandler<CleanStaleCacheFilesCommand, Task<int>>
{
    private readonly string _tempPath = ipathOpts.Value.TempDataPath;

    public async Task<int> Handle(CleanStaleCacheFilesCommand request, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-request.DaysOld);
        var tempDir = new DirectoryInfo(_tempPath);
        if (!tempDir.Exists) return 0;

        var candidates = new Dictionary<Guid, FileSystemInfo>();

        foreach (var fi in tempDir.GetFiles())
        {
            if (Guid.TryParse(fi.Name, out var docId) && fi.CreationTimeUtc < cutoff)
                candidates.TryAdd(docId, fi);
        }

        foreach (var di in tempDir.GetDirectories())
        {
            var dirName = di.Name;
            if (dirName.EndsWith("_files"))
                dirName = dirName[..^6];

            if (Guid.TryParse(dirName, out var docId) && di.CreationTimeUtc < cutoff)
                candidates.TryAdd(docId, di);
        }

        if (candidates.Count == 0) return 0;

        var docIds = candidates.Keys.ToList();

        var docs = await db.Documents
            .Where(d => docIds.Contains(d.Id))
            .Include(d => d.ServiceRequest)
                .ThenInclude(sr => sr.LastVisits)
            .Select(d => new
            {
                d.Id,
                LastSrVisit = d.ServiceRequest.LastVisits
                    .Max(v => (DateTime?)v.Date)
            })
            .ToListAsync(ct);

        var deleted = 0;
        foreach (var doc in docs)
        {
            if (doc.LastSrVisit.HasValue && doc.LastSrVisit.Value >= cutoff)
                continue;

            var idStr = doc.Id.ToString();
            var tempFile = Path.Combine(_tempPath, idStr);
            var dziFolder = Path.Combine(_tempPath, $"{idStr}_files");

            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                    deleted++;
                    logger.LogInformation("Cache hygiene: deleted {Path}", tempFile);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache hygiene: failed to delete {Path}", tempFile);
            }

            try
            {
                if (Directory.Exists(dziFolder))
                {
                    Directory.Delete(dziFolder, true);
                    deleted++;
                    logger.LogInformation("Cache hygiene: deleted {Path}", dziFolder);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache hygiene: failed to delete {Path}", dziFolder);
            }
        }

        return deleted;
    }
}
