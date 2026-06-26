using iPath.Application.Features.Conversion;

namespace iPath.Application.Contracts;

public interface IConversionPlugin
{
    bool CanHandle(string extension);

    IReadOnlyList<string> GetRequiredCompanions(string fileName);

    Task<ConversionResult> ProcessAsync(ConversionJobContext context, CancellationToken ct);
}
