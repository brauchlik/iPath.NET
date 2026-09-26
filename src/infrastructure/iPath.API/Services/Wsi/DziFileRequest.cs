using System.Text.RegularExpressions;

namespace iPath.API.Services.Wsi;

public enum DziFileKind { Raw, Descriptor, Tile }

/// <summary>
/// A parsed <c>documents/files/…</c> path: <c>{id}</c> (raw file), <c>{id}.dzi</c> (descriptor)
/// or <c>{id}_files/{level}/{col}_{row}.{ext}</c> (tile). The shape is fixed by the viewers and by
/// CaseRoomTokenAuthMiddleware.
/// </summary>
public sealed partial record DziFileRequest(Guid DocumentId, DziFileKind Kind, int Level = 0, int Column = 0, int Row = 0, string? Extension = null)
{
    [GeneratedRegex(@"^(\d{1,3})/(\d{1,9})_(\d{1,9})\.(webp|jpeg|jpg|png)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TilePath();

    public static DziFileRequest? Parse(string filepath)
    {
        var slash = filepath.IndexOf('/');
        var target = slash < 0 ? filepath : filepath[..slash];
        var rest = slash < 0 ? "" : filepath[(slash + 1)..];

        if (rest.Length == 0)
        {
            if (target.EndsWith(".dzi", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(target[..^4], out var descriptorId))
                return new DziFileRequest(descriptorId, DziFileKind.Descriptor);
            if (Guid.TryParse(target, out var rawId))
                return new DziFileRequest(rawId, DziFileKind.Raw);
            return null;
        }

        if (!target.EndsWith("_files", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(target[..^6], out var tileDocId))
            return null;

        var match = TilePath().Match(rest);
        if (!match.Success)
            return null;

        return new DziFileRequest(tileDocId, DziFileKind.Tile,
            int.Parse(match.Groups[1].ValueSpan),
            int.Parse(match.Groups[2].ValueSpan),
            int.Parse(match.Groups[3].ValueSpan),
            match.Groups[4].Value.ToLowerInvariant());
    }

    /// <summary>Path of the same file in a loose (unzipped) DZI output folder.</summary>
    public string LoosePath(string root) => Kind switch
    {
        DziFileKind.Descriptor => Path.Combine(root, $"{DocumentId}.dzi"),
        DziFileKind.Tile => Path.Combine(root, $"{DocumentId}_files", Level.ToString(), $"{Column}_{Row}.{Extension}"),
        _ => throw new InvalidOperationException("Raw files have no loose DZI path."),
    };
}
