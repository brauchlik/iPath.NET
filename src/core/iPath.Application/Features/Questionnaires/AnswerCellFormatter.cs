namespace iPath.Application.Features.Questionnaires;

/// <summary>
/// Renders an extracted answer as display text. Shared by the per-case dialog, the pivot table and
/// (later) the export, so the same answer cannot end up looking different in different places.
/// </summary>
public static class AnswerCellFormatter
{
    public static string Format(string? valueType, string? value, string? valueDisplay, string? unit)
    {
        // a coded answer is meant to be read as its label; the code itself lives in the column header
        if (string.Equals(valueType, "coding", StringComparison.OrdinalIgnoreCase))
        {
            return valueDisplay ?? value ?? string.Empty;
        }

        var text = value ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return string.IsNullOrEmpty(unit) ? text : $"{text} {unit}";
    }

    /// <summary>Repeating items produce one row per value; the table shows them in a single cell.</summary>
    public static string JoinValues(IEnumerable<string?> values)
        => string.Join("; ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
}
