namespace iPath.Application.Features.Admin;

public class PurgeDocumentFileDto
{
    public Guid DocumentId { get; set; }
    public string? Filename { get; set; }
    public DateTime? DeletedOn { get; set; }
    public bool HasTempFile { get; set; }
    public bool HasDziFolder { get; set; }
    public bool HasStagingDir { get; set; }
    public long TempFileSize { get; set; }
    public long DziFolderSize { get; set; }
    public long StagingDirSize { get; set; }
}

public class StaleCacheFileDto
{
    public Guid DocumentId { get; set; }
    public string? Filename { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? LastSrVisit { get; set; }
    public long TempFileSize { get; set; }
    public bool HasDziFolder { get; set; }
    public long DziFolderSize { get; set; }
    public long TotalSize { get; set; }
}
