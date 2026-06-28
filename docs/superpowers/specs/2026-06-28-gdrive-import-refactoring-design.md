# Google Drive Import System Refactoring

**Date:** 2026-06-28
**Status:** Draft

## Motivation

The current Google Drive import system was built as a prototype and has accumulated
technical debt: a fat interface (`IRemoteStorageService`) that mixes storage backend and
upload folder concerns, duplicated DocumentNode creation logic across import paths,
missing cleanup handlers, and several concrete bugs. This spec defines a clean
refactoring that preserves the WSI move optimization while establishing proper
boundaries.

## Design Summary

Three changes:
1. **Interface split** — `IRemoteStorageService` → `IStorageService` + `IUploadFolderService`
2. **Shared factory** — `DocumentNodeFactory` used by both upload paths
3. **Cleanup hooks** — cascade delete on request/user removal, periodic stale-folder cleanup

Plus fixes to all identified bugs.

---

## 1. Interface Split

### Current

```
IRemoteStorageService
├── ProviderName, RootStorageName, InitStorageAsync   ← storage CRUD
├── PutFileAsync / GetFileAsync / DeleteFileAsync
├── PutServiceRequestJsonAsync / DeleteServiceRequestJsonAsync  ← JSON export
├── CreateViewLink                                  ← view link
├── RenameRequest / RenameGroup / RenameCommunity   ← folder renames
├── UserUploadFolderActive                          ← upload folder CRUD
├── CreateUserUploadFolder / DeleteUserUploadFolder
├── CreateRequestUploadFolder / DeleteRequestUploadFolder
├── ScanUploadFolder / ImportUploadFolder            ← import
```

18 methods. `LocalStorageService` throws `NotImplementedException` for 7 of them.

### Proposed

```csharp
// src/core/iPath.Application/Contracts/IStorageService.cs
public interface IStorageService
{
    string ProviderName { get; }
    string RootStorageName { get; }
    Task<bool> InitStorageAsync();
    Task<StorageResponse> PutFileAsync(Guid id, CancellationToken ct);
    Task<StorageResponse> GetFileAsync(Guid id, CancellationToken ct);
    Task<StorageResponse> DeleteFileAsync(Guid id, CancellationToken ct);
    Task<string?> CreateViewLink(DocumentNode doc, CancellationToken ct);
    Task RenameRequest(ServiceRequest request);
    Task RenameGroup(Group group);
    Task RenameCommunity(Community community);
}

// src/core/iPath.Application/Contracts/IUploadFolderService.cs
public interface IUploadFolderService
{
    string ProviderName { get; }
    bool UserUploadFolderActive { get; }
    Task<UserUploadFolder> CreateUserUploadFolderAsync(Guid userId, CancellationToken ct);
    Task DeleteUserUploadFolderAsync(Guid userId, CancellationToken ct);
    Task<ServiceRequestUploadFolder> CreateRequestUploadFolderAsync(Guid requestId, Guid userId, CancellationToken ct);
    Task DeleteRequestUploadFolderAsync(Guid folderId, CancellationToken ct);
    Task<ScanExternalDocumentResponse> ScanUploadFolderAsync(ServiceRequestUploadFolder folder, CancellationToken ct);
    Task<FolderImportResponse> ImportUploadFolderAsync(ServiceRequestUploadFolder folder, IReadOnlyList<string>? storageIds, CancellationToken ct);
}
```

- `GoogleDriveStorageService` implements **both** interfaces
- `LocalStorageService` implements only `IStorageService`
- DI registration: `AddGoogleDriveServices` registers both; fallback registers only `IStorageService`
- `StorageRepsonse` renamed to `StorageResponse` throughout

### Cleanup: Stale Surface Area

| Removed | Reason |
|---------|--------|
| `PutServiceRequestJsonAsync` / `DeleteServiceRequestJsonAsync` | Never implemented, both throw `NotImplementedException`. Not part of the current domain. |
| Commented-out `ScanNewFilesAsync` / `ImportNewFilesAsync` | Replaced by `ScanUploadFolderAsync` / `ImportUploadFolderAsync` |

---

## 2. Shared DocumentNodeFactory

### Problem

Both `UploadNodeFileHandler` (browser/email upload) and `ImportUploadFolderAsync` (GDrive
import) create `DocumentNode` entities with the same type-detection logic (image vs WSI vs
file), thumbnail handling, and WSI conversion staging. The code is duplicated.

### Solution: `DocumentNodeFactory`

```csharp
// src/core/iPath.Application/Features/Documents/DocumentNodeFactory.cs
public class DocumentNodeFactory(IMimetypeService mime, IOptions<iPathClientConfig> clientOpts)
```

**Method:**

`DocumentNode Create(Guid serviceRequestId, Guid? parentId, Guid ownerId, string filename, string? mimeType, int sortNr)`

Creates a `DocumentNode` with:
- `Id` = `Guid.CreateVersion7()`
- `ServiceRequestId`, `ParentNodeId`, `CreatedOn`, `OwnerId`
- `SortNr` (passed in)
- `File` = new `NodeFile` with `Filename`, `MimeType`
- `DocumentType` detected via: image MIME → `"image"`, WSI extension → `"wsi"`, else `"file"`

The factory is **storage-agnostic** — no temp paths, no GDrive IDs, no view links.
Those remain in the callers.

### Caller divergence after creation

| Concern | Browser/Email (`UploadNodeFileHandler`) | GDrive (`ImportUploadFolderAsync`) |
|---------|-----------------------------------------|------------------------------------|
| File source | Saves stream to `TempDataPath/{id}` | File already on GDrive, moves it |
| Thumbnail | `srvThumb.UpdateNodeAsync` from local file | `GetThumbnailBase64Async` from GDrive link |
| View link | Created later via async `PutFileAsync` | `CreateViewLink` / `CreatePublicRangeLinkAsync` immediately |
| WSI companion folders | Copied from local `FilePath` directory | Downloaded from GDrive via `DownloadFolderContentsAsync` |

---

## 3. Cleanup Hooks

### 3a. Cascade on Request Delete

File: `DeleteServiceRequestHandler.cs`

When a `ServiceRequest` is deleted, also:
1. Delete all `ServiceRequestUploadFolder` DB records linked to this request
2. For each, delete the corresponding GDrive subfolder via `DeleteFolderAsync`

Call `IUploadFolderService.DeleteRequestUploadFolderAsync()` — if not registered (local
storage), skip.

### 3b. Cascade on User Delete / Google Disconnect

File: `DeleteUserCommandHandler.cs`

When a `User` is deleted:
1. Delete all `UserUploadFolder` DB records for this user
2. For each, recursively delete the GDrive folder tree (all child
   `ServiceRequestUploadFolder` folders)
3. Cascading DB delete removes child `ServiceRequestUploadFolder` records via EF
   `OnDelete(ClientCascade)`

Same for Google disconnect — reuse `DeleteUserUploadFolderCommandHandler` (called from UI when user unlinks Google).

### 3c. Periodic Cleanup of Stale Upload Folders

File: `SystemCleanupWorker.cs`

New config property in `SystemCleanupConfig`:

```csharp
public class SystemCleanupConfig
{
    // ... existing properties ...
    public bool CleanStaleUploadFolders { get; set; } = true;
    public int StaleUploadFolderDays { get; set; } = 30;
}
```

New command/handler pair:

- `CleanStaleUploadFoldersCommand`
- Finds `ServiceRequestUploadFolder` records where `CreatedOn < now - N days` AND the
  folder on GDrive contains no files
- Deletes folder from GDrive + DB record
- Logs count cleaned

---

## 4. Bug Fixes

| # | File | Line(s) | Bug | Fix |
|---|------|---------|-----|-----|
| 1 | `GoogleDriveStorage.cs` | 703 | `CreateUserUploadFolderAsync` returns `folder` (null when new) instead of `uFolder` | Return `uFolder` |
| 2 | `DeleteServiceRequestUploadFolderCommand` | — | No handler registered | Add handler (`CreateServiceRequestUploadFolderHandler` pattern) |
| 3 | `ScanExternalDocumentsQueryHandler.cs` | 19 | Returns `null` | Remove handler + query — unused, UI `DocumentImportDialog` is a stub |
| 4 | `GoogleDriveStorage.cs` | 754-761 | `DeleteRequestUploadFolderAsync` / `ScanUploadFolderAsync` throw `NotImplementedException` | Implement |
| 5 | `GoogleDriveStorage.cs` | 507-508 | Unreachable code after `return` | Remove dead `return` |
| 6 | `LocalStorageService.cs` | 77 | Inverted condition: `if (!File.Exists(...)) File.Delete(...)` | `if (File.Exists(...))` |
| 7 | `GoogleDriveStorage.cs` | 478 | Missing CancellationToken in `Permissions.Create` | Add cancellation token |
| 8 | `GDriveImportScanner.cs` | 28-38 | `StartAsync` calls `StopAsync` when disabled | Use `ExecuteAsync` check only |
| 9 | `iPathDbContext.cs` / `CommunitySettings.cs` | — | `Community.CaseTypes` primitive collection can be NULL in DB, EF bypasses C# property getter and throws on save | Add null guard in `SaveChangesAsync` interceptor OR configure `HasDefaultValue([])` in EF model + migration |

### Bug 9 Detail: Null CaseTypes on Save

EF Core 9 maps `ICollection<string> CaseTypes` as a JSON column. When the column is
literal `NULL` (e.g. from old SyncImport data or schema upgrade), EF accesses the
backing field `_caseTypes` directly — bypassing the C# getter's `??=` guard — and sees
null. At `SaveChanges` time, EF rejects the required (non-nullable) primitive collection.

**Fix (zero-migration, safe):** add a null-coalescing guard in `iPathDbContext.SaveChangesAsync`
before the base save:

```csharp
foreach (var entry in ChangeTracker.Entries())
{
    if (entry.Entity is Community c && c.Settings.CaseTypes is null)
        c.Settings.CaseTypes = [];
    if (entry.Entity is Group g && g.Settings.CaseTypes is null)
        g.Settings.CaseTypes = [];
}
```

---

## 5. Non-GDrive Scenarios

When Google Drive is not configured:
- `IUploadFolderService` is **not registered** in DI
- `IStorageService` is `LocalStorageService`
- Upload folder UI is hidden — `DocumentImportEnabled` returns `false` (existing behavior
  via `UserUploadFolderActive`)
- No change needed: the existing `UserUploadFolderActive` property already controls this

---

## 6. Files Changed

### Core: Interface & Factory (new/modified)

| File | Change |
|------|--------|
| `src/core/iPath.Application/Contracts/IStorageService.cs` | **New** — extracted from `IRemoteStorageService` |
| `src/core/iPath.Application/Contracts/IUploadFolderService.cs` | **New** — extracted from `IRemoteStorageService` |
| `src/core/iPath.Application/Contracts/IRemoteStorageService.cs` | **Deleted** |
| `src/core/iPath.Application/Features/Documents/DocumentNodeFactory.cs` | **New** |
| `src/core/iPath.Domain/Config/SystemCleanupConfig.cs` | **Modified** — add `CleanStaleUploadFolders`, `StaleUploadFolderDays` |

### Infrastructure: Implementation

| File | Change |
|------|--------|
| `src/infrastructure/iPath.Google/Storage/GoogleDriveStorage.cs` | **Modified** — implement both interfaces, fix bugs 1+4+5+7 |
| `src/infrastructure/iPath.API/Services/Storage/LocalStorageService.cs` | **Modified** — implement only `IStorageService`, fix bug 6 |
| `src/infrastructure/iPath.Google/GoogleServicesRegistration.cs` | **Modified** — register both interfaces |
| `src/infrastructure/iPath.API/APIServicesRegistration.cs` | **Modified** — fallback registers only `IStorageService` |
| `src/infrastructure/iPath.API/Services/Storage/GDriveImportScanner.cs` | **Modified** — fix bug 8 |
| `src/infrastructure/iPath.API/Services/Jobs/SystemCleanupWorker.cs` | **Modified** — add stale upload folder cleanup |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Documents/Commands/UploadNodeFileHandler.cs` | **Modified** — use `DocumentNodeFactory` |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/ServiceRequests/Commands/DeleteServiceRequestHandler.cs` | **Modified** — cascade to upload folders |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Users/Commands/DeleteUserCommandHandler.cs` | **Modified** — cascade upload folders |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/ServiceRequests/Commands/DeleteServiceRequestUploadFolderHandler.cs` | **New** — implement missing handler |
| `src/infrastructure/iPath.Application/*/CleanStaleUploadFoldersCommand.cs` | **New** — command + handler |

### Application: Commands / Handlers

| File | Change |
|------|--------|
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/ServiceRequests/Queries/ScanExternalDocumentsQueryHandler.cs` | **Removed** — dead query, UI dialog never calls it |
| Command/handler files referencing `IRemoteStorageService` | **Modified** — update to `IStorageService` or `IUploadFolderService` |
| `import/GoogleDriveAccess/` | **Deleted** — abandoned skeleton |

### Infrastructure: DI Registration

| File | Change |
|------|--------|
| All handlers injecting `IRemoteStorageService` | **Modified** — inject the appropriate interface |

---

## 7. Risk & Migration

- **No schema changes** — only C# refactoring. DB unchanged.
- **`IRemoteStorageService` is used in ~15 files** — each needs to be audited for which
  interface it actually needs. Most command handlers need only `IUploadFolderService`;
  `RemoteStorageUploadWorker` and `GoogleProxyEndpoints` need only `IStorageService`.
- **Worst case**: a handler currently using document CRUD + upload folder methods. These
  will need both interfaces injected. Expected to be rare.
