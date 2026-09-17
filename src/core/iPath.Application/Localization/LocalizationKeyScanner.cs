using System.Text.RegularExpressions;

namespace iPath.Application.Localization;

public class NonLiteralCallSite
{
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public string Snippet { get; set; } = string.Empty;
}

public class SourceScanResult
{
    public HashSet<string> Keys { get; set; } = new();
    public List<NonLiteralCallSite> NonLiteralCallSites { get; set; } = new();
    public int FilesScanned { get; set; }
    public int CallSitesScanned { get; set; }
}

/// <summary>
/// Statically scans the UI source tree for @T[...]/T[...] call sites and diffs the resulting
/// key set against locale JSON, so drift can be found and fixed without running the app or
/// clicking through every page in every language.
/// </summary>
public static class LocalizationKeyScanner
{
    // T["..."] / @T["..."] - the string literal itself never spans multiple lines in this codebase.
    private static readonly Regex LiteralCallRegex = new(@"\bT\[\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    // Any T[ call whose argument isn't a string literal, e.g. T[Item.Data.Type.ToString()].
    // Excludes "T[]" (generic array syntax, e.g. "public static T[] ToArray<T>(...)").
    private static readonly Regex NonLiteralCallRegex = new(@"\bT\[\s*(?!""|\])", RegexOptions.Compiled);

    public static SourceScanResult ScanSourceForKeys(string uiSourceRoot)
    {
        var result = new SourceScanResult();

        var files = Directory.EnumerateFiles(uiSourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        foreach (var file in files)
        {
            result.FilesScanned++;
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                foreach (Match m in LiteralCallRegex.Matches(line))
                {
                    result.CallSitesScanned++;
                    result.Keys.Add(Regex.Unescape(m.Groups[1].Value));
                }
                foreach (Match m in NonLiteralCallRegex.Matches(line))
                {
                    result.NonLiteralCallSites.Add(new NonLiteralCallSite { File = file, Line = i + 1, Snippet = line.Trim() });
                }
            }
        }

        return result;
    }

    public static (List<string> Missing, List<string> Orphaned) Diff(IReadOnlySet<string> sourceKeys, TranslationData locale)
    {
        var missing = sourceKeys.Where(k => !locale.Words.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var orphaned = locale.Words.Keys.Where(k => !sourceKeys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        return (missing, orphaned);
    }

    /// <summary>
    /// Appends missing keys (and, if requested, removes orphaned ones) directly on the given
    /// TranslationData. Caller is responsible for persisting via LocalizationFileService.SaveTranslation.
    /// </summary>
    public static void ApplyChanges(TranslationData locale, List<string> missingKeys, List<string> orphanedKeys, bool purge)
    {
        foreach (var key in missingKeys)
        {
            locale.Words[key] = locale.locale == "en" ? key : string.Empty;
        }

        if (purge)
        {
            foreach (var key in orphanedKeys)
            {
                locale.Words.TryRemove(key, out _);
                locale.WordMetadata.TryRemove(key, out _);
            }
        }
    }
}
