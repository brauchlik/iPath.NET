using iPath.Domain.Config;

namespace iPath.Application.Contracts.Storage;

/// <summary>
/// Key layout for LocalFiles and S3: <c>{groupId}/{requestId}/{documentId}/{variant}</c>, so a
/// group or case is one folder / prefix for backup and purge. The key is stored with the file and
/// never re-derived, so moving a case to another group does not move its files.
/// </summary>
public static class StorageKeys
{
    public const string OriginalVariant = "original";

    public static string ForDocument(Guid groupId, Guid requestId, Guid documentId, string variant = OriginalVariant) =>
        $"{groupId}/{requestId}/{documentId}/{variant}";

    /// <summary>
    /// Human-readable key for storage people browse directly (Google Drive):
    /// Community/Group/Case/File, each with a short id so equal names never collide and a folder
    /// can be traced back to its record. Names are taken as they are when the file is written.
    /// </summary>
    public static string Readable(string? community, Guid? communityId, string group, Guid groupId,
        string caseTitle, Guid requestId, string filename, Guid documentId)
    {
        var ext = Path.GetExtension(filename);
        var stem = Path.GetFileNameWithoutExtension(filename);
        var parts = new List<string>();
        if (communityId.HasValue)
            parts.Add($"{Clean(community)}__{Short(communityId.Value)}");
        parts.Add($"{Clean(group)}__{Short(groupId)}");
        parts.Add($"{Clean(caseTitle)}__{Short(requestId)}");
        parts.Add($"{Clean(stem)}__{Short(documentId)}{ext}");
        return string.Join('/', parts);
    }

    // The random tail of a v7 id: its head is a timestamp and repeats for ids created close together.
    private static string Short(Guid id) => id.ToString("N")[^8..];

    private static string Clean(string? name)
    {
        var cleaned = new string((name ?? "").Trim().Select(c => c is '/' or '\\' || char.IsControl(c) ? '-' : c).ToArray());
        return cleaned.Length == 0 ? "unnamed" : cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }

    /// <summary>
    /// Key for a file newly written to <paramref name="provider"/>: readable folders on Google
    /// Drive, id-based everywhere else. Needs the document's case, group and community loaded.
    /// </summary>
    public static string ForNewFile(IStorageProvider provider, DocumentNode document) =>
        provider.Type == StorageInstanceType.GoogleDrive
            ? Readable(document.ServiceRequest.Group?.Community?.Name, document.ServiceRequest.Group?.CommunityId,
                document.ServiceRequest.Group?.Name ?? "group", document.ServiceRequest.GroupId,
                document.ServiceRequest.Description?.Title ?? "case", document.ServiceRequestId,
                document.File.Filename ?? "file", document.Id)
            : ForDocument(document.ServiceRequest.GroupId, document.ServiceRequestId, document.Id);

    /// <summary>Where the tile index of a DZI zip is stored: next to the zip.</summary>
    public static string TileIndexFor(string key) => key + ".tileindex";

    /// <summary>
    /// The storage key of a stored file. Records written before named instances hold only a file
    /// name, stored under the case folder of the group at that time.
    /// </summary>
    public static string Resolve(StorageInfo storage, Guid groupId, Guid requestId) =>
        IsLegacy(storage) ? $"{groupId}/{requestId}/{storage.StorageId}" : storage.StorageId;

    /// <summary>A record written before full keys were stored: only a file name, located by the case's group.</summary>
    public static bool IsLegacy(StorageInfo storage) => !storage.StorageId.Contains('/');
}
