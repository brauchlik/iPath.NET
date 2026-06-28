using iPath.Application.Features.Admin;
using iPath.Domain.Config;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DispatchR.Abstractions.Send;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetDeletedDocumentsWithFilesHandler(
    iPathDbContext db,
    IOptions<iPathConfig> ipathOpts,
    IOptions<WsiConversionConfig> wsiOpts)
    : IRequestHandler<GetDeletedDocumentsWithFilesQuery, Task<List<PurgeDocumentFileDto>>>
{
    private readonly string _tempPath = ipathOpts.Value.TempDataPath;
    private readonly string _stagingPath = wsiOpts.Value.StagingPath;

    public async Task<List<PurgeDocumentFileDto>> Handle(GetDeletedDocumentsWithFilesQuery request, CancellationToken ct)
    {
        var docs = await db.Documents
            .IgnoreQueryFilters()
            .Where(d => d.DeletedOn != null && d.PurgedOn == null)
            .Select(d => new { d.Id, d.File!.Filename, d.DeletedOn })
            .ToListAsync(ct);

        return docs.Select(d =>
        {
            var id = d.Id.ToString();
            var tempFile = Path.Combine(_tempPath, id);
            var dziFolder = Path.Combine(_tempPath, $"{id}_files");
            var stagingDir = string.IsNullOrEmpty(_stagingPath) ? null : Path.Combine(_stagingPath, id);

            return new PurgeDocumentFileDto
            {
                DocumentId = d.Id,
                Filename = d.Filename,
                DeletedOn = d.DeletedOn,
                HasTempFile = File.Exists(tempFile),
                HasDziFolder = Directory.Exists(dziFolder),
                HasStagingDir = stagingDir != null && Directory.Exists(stagingDir),
                TempFileSize = File.Exists(tempFile) ? new FileInfo(tempFile).Length : 0,
                DziFolderSize = Directory.Exists(dziFolder) ? DirSize(dziFolder) : 0,
                StagingDirSize = stagingDir != null && Directory.Exists(stagingDir) ? DirSize(stagingDir) : 0
            };
        }).ToList();
    }

    private static long DirSize(string path) =>
        Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
}
