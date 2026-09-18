namespace VsiConverter.UI.Models;

public record AvailableSeries(
    int Index,
    int Width,
    int Height,
    double PixelSizeX,
    string? Description)
{
    public override string ToString()
        => $"Series {Index}: {Width}x{Height}{(Description is not null ? $" ({Description})" : "")}";
}
