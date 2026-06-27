using iPath.Application.Features.Conversion;

namespace iPath.Application.Contracts;

public interface IConversionPlugin
{
    bool CanHandle(string extension);

    IReadOnlyList<string> GetRequiredCompanions(string fileName);

    Task<ConversionResult> ProcessAsync(ConversionJobContext context, CancellationToken ct);

    Task<ThumbnailResult> CreateThumbnailAsync(ThumbnailContext context, CancellationToken ct);
}

public record ThumbnailContext(
    Guid DocumentId,
    string SourcePath,
    string TempDataPath,
    int ThumbSize,
    Domain.Entities.DocumentNode Document
);

public record ThumbnailResult(bool Success, string? ErrorMessage = null)
{
    public static ThumbnailResult Ok() => new(true);
    public static ThumbnailResult Fail(string message) => new(false, message);
}
