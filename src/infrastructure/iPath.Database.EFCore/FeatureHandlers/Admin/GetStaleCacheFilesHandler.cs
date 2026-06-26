using iPath.Application.Features.Admin;
using iPath.Domain.Config;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DispatchR.Abstractions.Send;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetStaleCacheFilesHandler(
    iPathDbContext db,
    IOptions<iPathConfig> ipathOpts)
    : IRequestHandler<GetStaleCacheFilesQuery, Task<List<StaleCacheFileDto>>>
{
    private readonly string _tempPath = ipathOpts.Value.TempDataPath;

    public async Task<List<StaleCacheFileDto>> Handle(GetStaleCacheFilesQuery request, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-request.DaysOld);
        var tempDir = new DirectoryInfo(_tempPath);
        if (!tempDir.Exists) return [];

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

        if (candidates.Count == 0) return [];

        var docIds = candidates.Keys.ToList();

        var docs = await db.Documents
            .Where(d => docIds.Contains(d.Id))
            .Include(d => d.ServiceRequest)
                .ThenInclude(sr => sr.LastVisits)
            .Select(d => new
            {
                d.Id,
                Filename = d.File != null ? d.File.Filename : null,
                d.CreatedOn,
                LastSrVisit = d.ServiceRequest.LastVisits
                    .Max(v => (DateTime?)v.Date)
            })
            .ToListAsync(ct);

        var result = new List<StaleCacheFileDto>();
        foreach (var doc in docs)
        {
            if (doc.LastSrVisit.HasValue && doc.LastSrVisit.Value >= cutoff)
                continue;

            var id = doc.Id.ToString();
            var hasDzi = Directory.Exists(Path.Combine(_tempPath, $"{id}_files"));
            var dziSize = hasDzi ? DirSize(Path.Combine(_tempPath, $"{id}_files")) : 0;
            var tempFileSize = candidates[doc.Id] is FileInfo fi ? fi.Length : 0;

            result.Add(new StaleCacheFileDto
            {
                DocumentId = doc.Id,
                Filename = doc.Filename,
                CreatedOn = doc.CreatedOn,
                LastSrVisit = doc.LastSrVisit,
                TempFileSize = tempFileSize,
                HasDziFolder = hasDzi,
                DziFolderSize = dziSize,
                TotalSize = tempFileSize + dziSize
            });
        }

        return result;
    }

    private static long DirSize(string path) =>
        Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
}
