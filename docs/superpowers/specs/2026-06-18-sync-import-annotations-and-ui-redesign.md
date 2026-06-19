# Sync Import: Annotations + UI Redesign

## Overview

Two changes to the live sync import feature:

1. **Import annotations** from old MySQL `annotation` table into the new `Annotation` entity
2. **Redesign the import page UI**: group list → detail page pattern, multi-select bulk sync, progress bars

---

## Part 1: Annotations Import

### Old MySQL Schema

Table `annotation` in old MySQL:
```
id          INT (PK)
sender_id   INT (FK → person)
object_id   INT (FK → objects)
data        TEXT (XML, double-encoded UTF-8)
entered     DATETIME
```

### New Entity

`Annotation` (namespace `iPath.Domain.Entities`):
- `ipath2_id` (int?) — old ID for re-run safety
- `ServiceRequestId` (Guid?) — links to root node
- `DcoumentNodeId` (Guid?) — links to child document
- `OwnerId` (Guid) — mapped from `sender_id`
- `CreatedOn` (DateTime) — mapped from `entered`
- `Data` (AnnotationData JSON) — `{ Type: Comment, Text: "...", ... }`

### Data Flow

| Old field | New field |
|-----------|-----------|
| `id` | `ipath2_id` |
| `sender_id` | `OwnerId` (via `_userIds` map) |
| `object_id` (root node) | `ServiceRequestId` (via `_nodeIds` map) |
| `object_id` (child node) | `ServiceRequestId` + `DcoumentNodeId` (via `_docIds` + `_docRootIds`) |
| `data` (XML) | `Data.Text` (parsed `/data/text` via LoadXml) |
| `entered` | `CreatedOn` |

If `object_id` resolves to neither `_nodeIds` nor `_docIds`, the annotation is skipped.

### File Changes

**SyncImportModels.cs** — add:
```csharp
public class OldAnnotationDto
{
    public int Id { get; set; }
    public int Sender_id { get; set; }
    public int Object_id { get; set; }
    public byte[]? Data { get; set; }
    public DateTime Entered { get; set; }
}
```

**OldDataService.cs** — add:
```csharp
public async Task<List<OldAnnotationDto>> GetAnnotationsForObjectsAsync(
    HashSet<int> objectIds, CancellationToken ct)
```
Reuses existing `BinaryDecode` const. SQL: `SELECT id, sender_id, object_id, {BinaryDecode} AS data, entered FROM annotation WHERE object_id IN @objectIds`

**SyncImportService.cs** — three changes:

1. **Add `_annotationIds`** dictionary + load it in `LoadIdMapsAsync`
2. **Add `ImportAnnotationsForObjectsAsync(HashSet<int> oldObjectIds, CancellationToken ct)`**
3. **Hook into `SyncGroupAsync`** after root import and after each document level import

---

## Part 2: Page Redesign

### SyncImportPage.razor — List View

**Route:** `@page "/admin/sync-import"`

**Layout:**
- Header + warning alert
- Toolbar: "Sync Selected (N)" button + progress bar
- MudDataGrid with multi-select, sort by Name, max-height 900px scrollable
- Row click → navigate to `/admin/sync-import/{id}`
- Per-row sync icon removed
- Multi-sync: loop selected groups, determinate progress bar

### SyncImportGroupPage.razor — Detail View

**Route:** `@page "/admin/sync-import/{id}"`

**Layout:**
- Breadcrumb: Admin > Sync Import > Group Name
- Summary card: root objects to import, annotations, users in group
- "Sync Now" button + indeterminate progress + completion message
- Status refreshed after sync

### Multi-Sync Progress

- Groups processed sequentially
- `MudProgressLinear Value` = `(completed / total) * 100`
- Label: "Syncing X of Y: GroupName ..."
- UI disabled during sync, re-enabled after

---

## File Manifest

### New files:
- `src/ui/iPath.RazorLib/Admin/SyncImport/SyncImportGroupPage.razor`

### Modified files:
- `src/core/iPath.Application/Features/SyncImport/SyncImportModels.cs` — add `OldAnnotationDto`
- `src/infrastructure/iPath.API/Services/SyncImport/OldDataService.cs` — add `GetAnnotationsForObjectsAsync`
- `src/infrastructure/iPath.API/Services/SyncImport/SyncImportService.cs` — add annotation import
- `src/ui/iPath.RazorLib/Admin/SyncImport/SyncImportPage.razor` — full redesign
