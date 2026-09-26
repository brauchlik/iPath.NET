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
