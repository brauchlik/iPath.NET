namespace iPath.Application.Features.Admin;

public record GetDocumentStorageInfoQuery(Guid DocumentId)
    : IRequest<GetDocumentStorageInfoQuery, Task<DocumentStorageInfoDto?>>;

public class DocumentStorageInfoDto
{
    public string? Filename { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public bool IsInCache { get; set; }
    public string? StorageProvider { get; set; }
    public string? StorageId { get; set; }
    public string? RemotePath { get; set; }
    public string? PublicUrl { get; set; }
    public DateTime? LastStorageExportDate { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? ConversionStatus { get; set; }
}
