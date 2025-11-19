namespace iPath.Blazor.Componenents.Extensions;

public static class UserExtensions
{
    public static string ToCSV(this string[]? values)
        => values is not null ? string.Join(", ", values) : String.Empty;
}
