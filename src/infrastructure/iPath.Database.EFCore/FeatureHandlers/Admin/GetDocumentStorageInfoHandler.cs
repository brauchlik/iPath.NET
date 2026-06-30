using iPath.Application.Contracts;
using iPath.Application.Features.Admin;
using iPath.Domain.Config;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using iPath.EF.Core.Database;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetDocumentStorageInfoHandler(
    iPathDbContext db,
    IOptions<iPathConfig> opts,
    IRemoteStorageService srvStorage)
    : IRequestHandler<GetDocumentStorageInfoQuery, Task<DocumentStorageInfoDto?>>
{
    public async Task<DocumentStorageInfoDto?> Handle(GetDocumentStorageInfoQuery request, CancellationToken ct)
    {
        var doc = await db.Documents
            .Include(d => d.ServiceRequest)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, ct);

        if (doc?.File is null) return null;

        var tempFile = Path.Combine(opts.Value.TempDataPath, doc.Id.ToString());

        string? remotePath = null;
        if (doc.File.Storage?.ProviderName == "LocalFiles" && doc.ServiceRequest is not null)
        {
            var dir = Path.Combine(opts.Value.LocalDataPath, doc.ServiceRequest.GroupId.ToString(), doc.ServiceRequest.Id.ToString());
            remotePath = Path.Combine(dir, doc.File.Storage.StorageId);
        }

        return new DocumentStorageInfoDto
        {
            Filename = doc.File.Filename,
            FileSize = doc.File.FileSize,
            MimeType = doc.File.MimeType,
            IsInCache = File.Exists(tempFile),
            StorageProvider = doc.File.Storage?.ProviderName,
            StorageId = doc.File.Storage?.StorageId,
            RemotePath = remotePath,
            PublicUrl = doc.File.PublicUrl,
            LastStorageExportDate = doc.File.LastStorageExportDate,
            ImageWidth = doc.File.ImageWidth,
            ImageHeight = doc.File.ImageHeight,
            ConversionStatus = doc.File.ConversionStatus?.ToString(),
            StorageProviderMismatch = doc.File.Storage?.ProviderName != srvStorage.ProviderName
        };
    }
}
