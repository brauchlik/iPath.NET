using iPath.Domain.Entities;

namespace iPath.Application.Features.Conversion;

public record ConversionJobContext(
    Guid DocumentId,
    string StagingPath,
    string OriginalFilename,
    string FileExtension,
    DocumentNode Document
);
