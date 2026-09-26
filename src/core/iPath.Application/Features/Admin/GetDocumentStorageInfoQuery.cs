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
    public DateTime? LastStorageExportDate { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? ConversionStatus { get; set; }
    /// <summary>The storage instance the document's community uses.</summary>
    public string? ExpectedStorage { get; set; }
    /// <summary>The file is not on its community's storage (yet): a migration is due or running.</summary>
    public bool? StorageProviderMismatch { get; set; }
}
