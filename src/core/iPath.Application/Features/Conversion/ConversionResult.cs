namespace iPath.Application.Features.Conversion;

public record ConversionResult(bool Success, string? ErrorMessage = null)
{
    public static ConversionResult Ok() => new(true);
    public static ConversionResult Fail(string message) => new(false, message);
}
