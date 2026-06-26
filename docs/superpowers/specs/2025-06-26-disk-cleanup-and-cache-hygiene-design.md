# Disk Cleanup & Cache Hygiene

## Overview

Two admin features to manage disk space on the server, added to the existing System Status page (`admin/system/status`):

1. **Purge Deleted Documents** — clean up files (temp + storage) for soft-deleted documents
2. **Cache Hygiene** — remove stale temp files for active documents whose parent ServiceRequest hasn't been visited recently

Both follow the existing `Query → Handler → Endpoint → Client` pattern used by `VsiConversionJobs`.

---

## Feature 1: Purge Deleted Documents

### What it does

Queries for documents where `DeletedOn IS NOT NULL`, checks if they still have files on disk (temp folder, DZI `_files/`, staging), and lets admins purge them — deleting temp files directly and calling `IRemoteStorageService.DeleteFileAsync()` for storage.

### Query

`GetDeletedDocumentsWithFilesQuery` → `List<PurgeDocumentFileDto>`

```sql
SELECT d.Id, d.File.Filename, d.DeletedOn
FROM Documents d
WHERE d.DeletedOn IS NOT NULL
```

For each result, check disk:
- `TempDataPath/{id}` — file exists?
- `TempDataPath/{id}_files/` — DZI folder exists?
- `StagingPath/{id}/` — staging dir exists?
- File sizes for each

### Command

`PurgeDocumentFilesCommand(Guid DocumentId)` — deletes:
1. `TempDataPath/{id}` (file)
2. `TempDataPath/{id}_files/` (DZI folder, recursive)
3. `StagingPath/{id}/` (staging dir, recursive)
4. Calls `IRemoteStorageService.DeleteFileAsync(id)` for storage cleanup

"Purge All" is a client-side loop.

### DTO

```csharp
public record PurgeDocumentFileDto(
    Guid DocumentId, string? Filename, DateTime? DeletedOn,
    bool HasTempFile, bool HasDziFolder, bool HasStagingDir,
    long TempFileSize, long DziFolderSize, long StagingDirSize
);
```

---

## Feature 2: Cache Hygiene

### What it does

Scans `TempDataPath` for files/folders named `{guid}` or `{guid}_files/` that are older than N days (by `CreationTimeUtc`). For each, checks if the parent ServiceRequest has been visited in the same window. If not visited → candidate for eviction.

### Query

`GetStaleCacheFilesQuery(int DaysOld = 7)` → `List<StaleCacheFileDto>`

1. Enumerate `TempDataPath` for `{guid}` files and `{guid}_files/` folders
2. Filter by `File.GetCreationTimeUtc() < now - DaysOld`
3. Batch query DB: find parent ServiceRequest and its LastVisits for each candidate
4. Return only those whose SR has no recent visit

### Command

`CleanStaleCacheFilesCommand(int DaysOld = 7)` — runs the full scan internally and deletes:
1. `TempDataPath/{id}` (file)
2. `TempDataPath/{id}_files/` (DZI folder, recursive)

Does NOT touch staging or remote storage.

### DTO

```csharp
public record StaleCacheFileDto(
    Guid DocumentId, string? Filename, DateTime CreatedOn,
    DateTime? LastSrVisit, long TempFileSize, bool HasDziFolder, long TotalSize
);
```

---

## File Changes

### New Files

| File | Purpose |
|------|---------|
| `src/core/iPath.Application/Features/Admin/PurgeDocumentDto.cs` | DTOs |
| `src/core/iPath.Application/Features/Admin/AdminPurgeQueries.cs` | Queries + Commands |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Admin/GetDeletedDocumentsWithFilesHandler.cs` | Deleted docs handler |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Admin/PurgeDocumentFilesHandler.cs` | Purge handler |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Admin/GetStaleCacheFilesHandler.cs` | Stale cache handler |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Admin/CleanStaleCacheFilesHandler.cs` | Clean handler |

### Modified Files

| File | Change |
|------|--------|
| `src/infrastructure/iPath.API/Endpoints/AdminEndpoints.cs` | 4 new endpoints |
| `src/ui/iPath.Blazor.ServiceLib/ApiClient/IApiClient.cs` | 4 new methods |
| `src/ui/iPath.Blazor.ServiceLib/Services/DirectApiClient.cs` | 4 new methods |
| `src/ui/iPath.RazorLib/Admin/SystemStatus.razor` | 2 new sections |

---

## UI (System Status Page)

Two MudDataGrid sections below the existing VSI jobs grid:

### Section 1: Deleted Documents with Files

| Column | Source |
|--------|--------|
| Filename | `Filename` |
| Deleted On | `DeletedOn` (g format) |
| Temp File | `HasTempFile` (yes/no MudChip) |
| DZI Folder | `HasDziFolder` (yes/no MudChip) |
| Temp Size | human-readable byte size |
| Storage | storage exists indicator |
| Action | Per-row "Purge" button + "Purge All" button |

### Section 2: Stale Cache Files

| Column | Source |
|--------|--------|
| Filename | `Filename` |
| Created On | `CreatedOn` (g format) |
| SR Last Visit | `LastSrVisit` or "Never" |
| Temp Size | `TempFileSize` human-readable |
| DZI | `HasDziFolder` |
| Action | "Clean Stale Cache" button |
