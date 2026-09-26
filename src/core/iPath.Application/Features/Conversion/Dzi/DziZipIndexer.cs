using System.Text.RegularExpressions;

namespace iPath.Application.Features.Conversion.Dzi;

public sealed record DziZipIndexResult(DziTileIndex? Index, string? Error, bool RequiresRepack)
{
    public bool Success => Index is not null;

    public static DziZipIndexResult Ok(DziTileIndex index) => new(index, null, false);
    public static DziZipIndexResult Fail(string error) => new(null, error, false);
    public static DziZipIndexResult Repack(string reason) => new(null, reason, true);
}

/// <summary>
/// Builds a <see cref="DziTileIndex"/> from a DZI zip (one <c>.dzi</c> descriptor plus its
/// <c>_files/</c> folder, at the root or inside a single top-level folder). Only the descriptor
/// and tile images are indexed, so nothing else in the zip — e.g. vips-properties.xml — can be
/// served.
/// </summary>
public static partial class DziZipIndexer
{
    [GeneratedRegex(@"^(\d{1,3})/(\d{1,9})_(\d{1,9})\.(webp|jpeg|jpg|png)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TilePath();

    public static DziZipIndexResult Build(Stream zip)
    {
        IReadOnlyList<ZipCentralEntry> entries;
        try
        {
            entries = ZipCentralDirectory.ReadEntries(zip);
        }
        catch (InvalidDataException ex)
        {
            return DziZipIndexResult.Fail($"Not a readable zip: {ex.Message}");
        }

        var files = entries
            .Select(e => (Entry: e, Path: ZipExtraction.NormalizeEntryPath(e.Name)))
            .Where(f => !f.Entry.IsDirectory && !f.Path.StartsWith("__MACOSX/", StringComparison.Ordinal))
            .ToList();

        var descriptors = files.Where(f => f.Path.EndsWith(".dzi", StringComparison.OrdinalIgnoreCase)).ToList();
        if (descriptors.Count != 1)
            return DziZipIndexResult.Fail($"Expected exactly one .dzi descriptor, found {descriptors.Count}.");

        var descriptor = descriptors[0];
        if (descriptor.Path.Count(c => c == '/') > 1)
            return DziZipIndexResult.Fail("The .dzi descriptor must be at the zip root or inside a single top-level folder.");

        var filesPrefix = descriptor.Path[..^".dzi".Length] + "_files/";
        var tiles = new List<(ZipCentralEntry Entry, int Level, int Column, int Row, string Extension)>();
        foreach (var file in files)
        {
            if (!file.Path.StartsWith(filesPrefix, StringComparison.Ordinal))
                continue;
            var match = TilePath().Match(file.Path[filesPrefix.Length..]);
            if (!match.Success)
                continue;
            tiles.Add((file.Entry,
                int.Parse(match.Groups[1].ValueSpan),
                int.Parse(match.Groups[2].ValueSpan),
                int.Parse(match.Groups[3].ValueSpan),
                NormalizeExtension(match.Groups[4].Value)));
        }

        if (tiles.Count == 0)
            return DziZipIndexResult.Fail($"No tiles found under '{filesPrefix}'.");

        var extensions = tiles.Select(t => t.Extension).Distinct().ToList();
        if (extensions.Count != 1)
            return DziZipIndexResult.Fail($"Mixed tile formats: {string.Join(", ", extensions)}.");

        var indexed = tiles.Select(t => t.Entry).Append(descriptor.Entry).ToList();
        if (indexed.Any(e => e.IsEncrypted))
            return DziZipIndexResult.Fail("Encrypted zip entries are not supported.");
        if (indexed.Any(e => !e.IsStored))
            return DziZipIndexResult.Repack("Zip entries are compressed; tiles can only be range-read from stored entries.");
        if (tiles.Any(t => t.Entry.CompressedSize > int.MaxValue))
            return DziZipIndexResult.Fail("A tile exceeds 2 GB.");

        var tileEntries = new List<DziTileIndex.TileEntry>(tiles.Count);
        foreach (var tile in tiles)
        {
            var range = ZipCentralDirectory.ResolveDataRange(zip, tile.Entry);
            tileEntries.Add(new DziTileIndex.TileEntry(tile.Level, tile.Column, tile.Row, range.Offset, (int)range.Length));
        }

        var descriptorRange = ZipCentralDirectory.ResolveDataRange(zip, descriptor.Entry);
        return DziZipIndexResult.Ok(new DziTileIndex(descriptorRange, extensions[0], tileEntries));
    }

    private static string NormalizeExtension(string extension)
    {
        var lower = extension.ToLowerInvariant();
        return lower == "jpg" ? "jpeg" : lower;
    }
}
