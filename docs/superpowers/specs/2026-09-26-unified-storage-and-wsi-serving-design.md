# Unified Binary Storage & WSI Serving — design capture

> **Status: proposed, decisions taken, not implemented.** Captured 2026-09-26 from a design
> discussion so the reasoning is not lost. No code exists for anything described here.
>
> **Sprint: general remote storage overhaul**, on branch `feature/remote-storage-overhaul`
> (from `origin/main`). The system is **not in production yet**: entities, storage records and
> config may change without backward compatibility, data conversion or cleanup jobs — dev data is
> re-imported instead.
>
> Builds on `2026-06-28-zip-import-plugin-cleanup.md` (canonical DZI zip) and
> `2025-06-26-disk-cleanup-and-cache-hygiene-design.md` (temp cache purge). Where they conflict,
> this document wins: serving from the stored zip removes the extraction cache those two assume.

## 1. Purpose

Binary data (images, whole-slide images) lives on local disk or Google Drive today; S3 (tested
against RustFS) is next. Three questions drive this document:

1. **Which storage holds which data?** One storage per installation, optionally overridden per
   community — never switched on the fly.
2. **Can storage and serving be written abstractly without losing efficiency?** Providers with very
   different access characteristics sit behind one interface.
3. **How do we compare approaches during development?** Same data, different providers and
   formats, measured under realistic (low-bandwidth) conditions.

## 2. What exists today (do not rebuild blindly)

| Piece | Where | Note |
|---|---|---|
| Storage contract | `src/core/iPath.Application/Contracts/IRemoteStorageService.cs` | Case-tree shaped: `PutFileAsync(Guid)`, `PutServiceRequestJsonAsync`, `RenameRequest/Group/Community`, `CreateViewLink`, user upload folders. One provider active per app instance (DI at startup). |
| Local provider | `src/infrastructure/iPath.API/Services/Storage/LocalStorageService.cs` | `ProviderName = "LocalFiles"`, `CanServeDirectly = true`, path `LocalDataPath/{groupId}/{requestId}/{storageId}`. |
| Drive provider | `src/infrastructure/iPath.Google/Storage/GoogleDriveStorage.cs` | ~950 lines; human-readable Community/Group/Case folders. Depends on `iPathDbContext`, `UserManager`, group cache — cannot be instantiated per configured Drive as is. |
| Drive config | `src/infrastructure/iPath.Google/GoogleDriveConfig.cs` | Single Drive: `ClientSecretPath`, `RootFolderId`, `UserUploadFolderId`. |
| Group → community | `Group.CommunityId` (main community, nullable) + `Group.ExtraCommunities` | The main community is what decides storage. |
| Location state | `StorageInfo` on `NodeFile`, `GroupSettings`, `CommunitySettings`, `RequestDescription` | Provider-internal location (e.g. Drive folder id). `NodeFile.Storage` is a single `StorageInfo?` inside the JSON column `file` (`DocumentNodeConfiguration.cs:24`). An **unused** `List<StorageInfo>` extension block already exists (`src/core/iPath.Domain/Entities/Base/StorageInfo.cs:29`). |
| Tile endpoint | `src/infrastructure/iPath.API/Endpoints/DocumentEndpoints.cs:66` (`files/{*filepath}`) | On cache miss extracts the **whole zip** into `TempDataPath`, inside the tile request, with no single-flight guard. Runs `GetDocumentFileQuery` (DB + access check) **per tile**. |
| Zip writers | `DziImportPlugin.cs:91`, `VsiConversionPlugin.cs:288`, `tools/VsiConverter/VsiConverter.UI/Services/PipelineRunner.cs:124` | All use `CompressionLevel.NoCompression` — entries are stored, not deflated. |
| WSI conversion | `WsiConversionPlugin.cs` | `bfconvert` → OME-TIFF → `vips dzsave` (254px, overlap 1, webp) written **loose** into `TempDataPath`, no zip. |
| DZI import | `DziImportPlugin.cs` | Extracts upload → renames to `{docId}.dzi`/`{docId}_files` → re-zips. Three writes of a large slide, only to canonicalise entry names. |

## 3. Storage topology

### 3.1 Resolution

```
file without community (avatars, logos, system files)   → local server storage, always
file of a case → group → group.CommunityId → community.StorageInstance ?? Storage:Default
```

- **Local server storage** is fixed and not configurable per community. It is distinct from
  `Storage:Default`, which may itself be S3 or a Drive.
- **Invariant: all data of a community lives in one storage instance.**
- Storage is chosen at startup and never switched in a running application. Changing it is a
  migration (§6).

### 3.2 Named instances

A provider is a *configured instance*, not a type — several Google Drives (one per community) are
possible:

```json
"Storage": {
  "Default": "local-main",
  "Instances": {
    "local-main":    { "Type": "LocalFiles",  "Path": "/data/ipath" },
    "gdrive-derma":  { "Type": "GoogleDrive", "ClientSecretPath": "...", "RootFolderId": "...", "UserUploadFolderId": "..." },
    "gdrive-africa": { "Type": "GoogleDrive", "ClientSecretPath": "...", "RootFolderId": "..." },
    "rustfs-dev":    { "Type": "S3", "ServiceUrl": "...", "Bucket": "ipath" }
  }
}
```

- Instances and their credentials come from appsettings / the secret store, loaded at startup.
  **Secrets never go into the database.**
- The community stores only the **instance name** (`CommunitySettings.StorageInstance`). That is a
  different thing from the existing `CommunitySettings.Storage`, which is location state *inside*
  a provider (the Drive folder id): "which storage" vs. "where in it".
- The instance name is read-only in the UI once the community has data; only the migration tool
  changes it.
- Today's single `GoogleDriveConfig` becomes the `GoogleDrive` instance type.

### 3.3 Google Drive specifics: user upload folders

A Drive-only way to bring files into a case without a browser upload (the Drive desktop client
handles large, resumable uploads on slow links):

1. `CreateUserUploadFolderAsync` creates a folder per user under the instance's
   `UserUploadFolderId` and shares it with the user's linked Google account.
2. `CreateRequestUploadFolderAsync` creates a per-case subfolder in it.
3. `GDriveImportScanner` (background service, `GDriveImportScanner:Enabled`, `IntervalMinutes`)
   calls `ImportUploadFolderAsync` for every case upload folder; the case UI can also trigger it.
4. Import creates a document per new file, a thumbnail, and queues WSI conversion (with companion
   folders for VSI).

Model:

- **Configured per user**: a user is assigned **0 or 1 Drive instance** for uploads.
  `UserUploadFolder.StorageProvider` holds the **instance name**. Only instances with a
  `UserUploadFolderId` can be assigned.
- The scanner resolves the Drive via the user: case upload folder → user upload folder → instance.
- Import depends on where the case's community lives:
  - **same Drive instance** → move (re-parent), as today;
  - **any other instance** → download, store via the normal path on the community's instance,
    delete from the upload folder.
- The upload folder is a capability of the Drive instance type, **not** part of the general
  storage contract.

## 4. Cost of abstraction

The interface dispatch is noise (ns vs. µs local / ms remote). Efficiency is lost in three places,
and each has a fix:

**a) Wrong primitive.** A "whole stream" contract forces full downloads; a "file path" contract
forces local copies of remote objects. **Range reads** are cheap on every backend:

```csharp
public interface IStorageProvider
{
    string InstanceName { get; }
    Task<StorageObjectInfo?> GetInfoAsync(string key, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task<BlobRange> GetRangeAsync(string key, long offset, long length, CancellationToken ct);
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct); // S3: multipart (> 5 GB)
    Task DeleteAsync(string key, CancellationToken ct);
}
```

**b) Losing local zero-copy.** Pushing local files through a managed `Stream` loses kernel
`sendfile`/`TransmitFile`. The provider therefore returns a range *description* and the endpoint
picks the transport:

```csharp
public abstract record BlobRange(long Offset, long Length);
public sealed record PhysicalFileRange(string Path, long Offset, long Length) : BlobRange(Offset, Length);
public sealed record StreamRange(Func<CancellationToken, Task<Stream>> Open, long Offset, long Length) : BlobRange(Offset, Length);
```

`PhysicalFileRange` → `HttpResponse.SendFileAsync(path, offset, count)`; `StreamRange` → pipe the
response stream unbuffered.

**c) Per-request metadata work — the biggest real cost today.** A viewer issues 20–50 tile
requests per second; each currently does a DB query + access check. Cache the access decision per
(user, document) and the tile index per document for ~1 minute (`HybridCache`).

### 4.1 Layering

| Layer | Lifetime | Knows about |
|---|---|---|
| **Blob provider** (`IStorageProvider`), one per instance | singleton, keyed DI by instance name | keys and bytes only — no DB |
| **Cache decorator**, optional per instance | singleton | see §5 |
| **Document storage service** | scoped | document → community → instance, key building, case JSON, Drive folder naming via a provider-specific extension, location records (§6) |
| **Tile reader** | scoped | tile index + `GetRangeAsync`; the only code that knows about DZI/zip |

Splitting the current `GoogleDriveStorageService` along these lines is required, not cosmetic: its
DB and `UserManager` dependencies prevent one instance per configured Drive.

## 5. Serving: always through the app

All data (tiles, images) is served through iPath for authorization. Direct access to S3/Drive
brings no significant gain in this deployment:

| Factor | Through the app | Direct |
|---|---|---|
| Latency | client→app RTT + app→storage (RustFS same host/LAN: < 1–5 ms) | client→S3 RTT; faster only if S3 is *closer to the client* than the app (CDN, other region) |
| Bandwidth | bytes pass the app uplink | saves app bandwidth only if S3 sits on another network |
| Authorization | one check per (user, document), cached | presigned URL per tile — OSD cannot sign client-side; presigning the zip does not help (OSD cannot read inside it) |
| Google Drive | works | not usable: private files need an OAuth token in the browser; request quotas |
| Old Apache proxies | one known path | second origin, CORS, another hole |

On slow high-latency links the real limit is RTT × concurrent connections (≈ 6 per host behind
HTTP/1.1 proxies); a second hostname for the app gives the same parallelism without exposing
storage.

Performance comes from, in order:

1. **Browser cache** — tiles already get `Cache-Control: public, max-age=31536000`; keep tile URLs
   stable (document-id based).
2. **Access decision + tile index cache** (§4c).
3. **Server cache per instance, chosen by the instance's latency** (cache decorator, configured per
   instance; tile reader and endpoint are unaware of it):
   - **LocalFiles** — no cache; the OS page cache already is one.
   - **S3/RustFS nearby** — none, or a small byte-bounded in-memory LRU for hot tiles. Measure first.
   - **Google Drive** — disk cache of **whole objects** (download the zip once, range-read locally,
     LRU eviction).
   - **Reuse the existing cache management** as the engine of the decorator, don't build a second
     one: `CacheManager.GetOrPrepareAsync` (`src/infrastructure/iPath.API/Services/Cache/CacheManager.cs`)
     with `DocumentCacheEntry` (cost class, state, `LastAccessed`/`AccessCount`), eviction, and the
     admin stale-cache/purge handlers. Changes: `DocumentCacheEntry.StorageProvider` holds the
     instance name; it caches the stored object (zip / original) instead of extracted tiles; the
     `CanServeDirectly` shortcut becomes "instance type LocalFiles → no cache".
   - Everything that used to go to a public Drive link (`PublicUrl`) is served through the app
     from this cache instead.

## 6. Location records and migration

### 6.1 Every stored object records where it lives

Each document records each stored object with its instance and status. This is a **location
history**, not a set of replicas: at most one `Active` location per variant; `Retired` entries are
source copies left behind by a migration.

```csharp
public class StorageLocation
{
    public string Instance { get; set; }                  // configured instance name
    public string Key { get; set; }                       // written once, never re-derived
    public string Variant { get; set; } = "Original";     // Original | DziZip | TileIndex
    public StorageLocationStatus Status { get; set; }     // Pending | Active | Retired | Purged
    public long? Size { get; set; }
    public string? Sha256 { get; set; }                   // verification during migration
    public DateTime? UpdatedOn { get; set; }
}
```

- Reads use the `Active` location of the requested variant.
- A document whose `Active` instance differs from its community's instance is either an unfinished
  migration or a bug — the admin consistency check reports it (today's `StorageProviderMismatch`
  flag is the seed of this).
- Storage: `NodeFile` holds `List<StorageLocation>` inside the JSON column `file`, replacing
  `Storage` outright (not in production → no data conversion). EF model migration; the developer
  runs `dotnet ef` per provider (AGENTS.md).

### 6.2 Keys and human readability

The Community/Group/Case layout exists so storage is understandable **without iPath**. That is
the **Google Drive option**: an installation or community that wants human-readable storage uses a
Drive instance.

- **Key scheme is per instance type:**
  - **GoogleDrive** — human-readable folders (Community/Group/Case/file), as today.
  - **S3 and LocalFiles** — **id-only keys, same layout on both**:
    `{groupId}/{requestId}/{documentId}/{variant}` (LocalFiles under `LocalDataPath`, S3 in the
    instance's bucket). No renames, no escaping of names, no collisions.
  - The group/case prefix exists for **operations and backup**: one group or case is one folder
    (local) or one prefix (S3, e.g. `mc mirror s3/ipath/{groupId} /backup/…`, `rclone`), so backup,
    copy, verify and purge work per group with standard tools and without the DB. No directory
    grows unbounded.
  - Because the key is stored, a case moved to another group keeps its old path; no file move.
- **S3 is kept as simple as possible**: a pure blob provider implementing only the
  `IStorageProvider` methods (§4) — one bucket per instance, the key is just a string, no
  folder objects, no case JSON, no object tags/metadata, no renames, no presigned URLs. Large uploads use
  the SDK's `TransferUtility`, which does multipart automatically. Works unchanged against RustFS,
  MinIO and AWS (`ServiceUrl` + `ForcePathStyle`).
- **The key is written once and stored.** On Drive, a later rename in iPath does not touch storage;
  the folder name goes stale until the next migration, which applies the current names while copying.
- Drive names get short id suffixes (`Dermatology__a1b2c3/Case 2024-117__d4e5f6/HE_slide__g7h8.svs`)
  — collision-free and traceable to the DB.
- The per-case JSON (`PutServiceRequestJsonAsync`) stays a Drive feature; it is what makes a Drive
  folder readable without the application.
- Native originals (`.svs`) open in QuPath/ImageScope; a `.dzi.zip` does not — on Drive,
  readability is served by the original variant.

### 6.3 Migration: rare, manual, complete

Migration is an **admin tool** (console command or hidden admin action) run in a maintenance
window — no UI workflow, no background scheduling.

Triggers:

- changing a community's storage instance;
- **moving a group into, out of, or between main communities** — always a migration, never just a
  DB update. Even with the same instance on both sides the tool runs, because the human-readable
  layout (Community/Group/…) changes; on Drive that is a cheap in-Drive folder move.

Steps, per community or group:

1. Block writes for the scope (maintenance).
2. For each object: add target location `Pending` → copy through the server → verify size + hash →
   target `Active`, source `Retired`. Resumable per object.
3. **Always write a local backup** of every object passing through:
   `{BackupRoot}/{groupId}/{requestId}/{documentId}/{variant}` — a self-contained per-group copy,
   whatever the source and target instance (Drive → S3 cannot copy server-side anyway, so the
   bytes pass the server regardless).
4. Switch the community's instance (or the group's `CommunityId`).
5. Consistency check: no `Active` location outside the scope's instance.
6. Later, separately and manually: purge `Retired` source objects **and** the backup → `Purged`.

Safety net: until step 6, rolling back is a status flip on the location records (no data copy),
and the backup folder is an independent copy of the group.

## 7. Comparing approaches during development

Falls out of the production design — no dev-only mechanism in the domain model:

1. **Dev config with several instances** (`local-dev`, `rustfs-dev`, `gdrive-dev`) and **one test
   community per instance**; import the same dataset into each. Comparison runs in one app,
   through exactly the production code path.
2. **Record & replay**: capture a real OSD viewing session (tile URL sequence from the server log),
   replay it with a small .NET console tool against each community's copy. Measure TTFB p50/p95,
   bytes, server CPU — with and without the cache decorator.
3. **Simulate the real link**: Toxiproxy between client and server (bandwidth + latency).
4. **RustFS in the Aspire AppHost** so every developer gets the same S3 endpoint.
5. BenchmarkDotNet only for micro questions (index lookup, local range read).

## 8. Serving DZI straight from the stored zip

Stored (uncompressed) entries are contiguous byte ranges, so a tile is a seek + copy.

- Build an index `tilePath → (dataOffset, length)` **at import** and store it (variant
  `TileIndex`). `dataOffset = localHeaderOffset + 30 + nameLength + localExtraLength` — read the
  extra length from the **local** header; it can differ from the central directory's.
- **ZIP64 is mandatory**: large slides exceed 65,535 entries and 4 GB. .NET writes ZIP64
  automatically; the index reader must parse it.
- Uploads already fully stored are kept **as uploaded**. Repack only if any entry is deflated
  (third-party zip tools). The index maps entry names, so the `{docId}` rename disappears.
- The index is compact (sorted binary records, 24 bytes per tile; measured: 57k tiles → 1.4 MB),
  loaded once per slide and kept in memory. The zip's own central directory is no substitute: ~10 MB
  for the same slide because of the names, and it points at local headers, so every tile would need
  a second read.
- Serving never opens the zip as a zip — no `ZipArchive`, no central directory, no extraction.

### 8.1 Remote zip: range reads (S3) vs. download first (Drive)

Same tile reader for every instance; the per-instance cache policy decides:

| | S3 / RustFS | Google Drive |
|---|---|---|
| Range read | native `GET` + `Range` | `files.get?alt=media` + `Range`, OAuth server-side |
| Latency per request | RustFS LAN < 5 ms; AWS same region ~10–30 ms | ~100–300 ms+ |
| Limits | none relevant at this load | per-user / per-project quotas (`userRateLimitExceeded`) |
| Viewer at 20–50 tiles/s | fine | quota and latency problems |
| **Policy** | **range-read tiles directly from the remote zip**, no download; optional small byte-bounded memory LRU if measurements justify it | **download the whole zip into the local cache first**, then local range reads |

Drive details:

- Download on first view, **single-flight per document**, the viewer shows "preparing"
  (`CacheManager`'s `Extracting` state becomes "downloading").
- **Warm at write time**: the zip is produced on the server (conversion/import), so it stays in the
  cache after the upload to Drive — the first view normally needs no download at all.
- Later refinement, not now: serve the coarse levels by remote range while the download runs.

LocalFiles: no cache, range reads on the file directly (`PhysicalFileRange`).

## 9. WSI format strategy: convert everything to DZI by default

| | All DZI | Native TIFF passthrough |
|---|---|---|
| Serving code | one path; OSD built-in DZI | TIFF IFD parser + custom OSD tile source |
| Coverage | everything vips/OpenSlide reads; VSI via bfconvert | tiled JPEG TIFF only (mostly `.svs`) |
| Bandwidth | webp, typically clearly smaller than svs JPEG at similar quality | original JPEG tiles |
| Pyramid | exact 2× per level | svs often 4× → custom level scales or on-the-fly downscale |
| Colour / label | clean | Aperio APP14 quirk; label/macro IFDs must be filtered |
| Cost | CPU per slide, wait before viewable, JPEG→webp generation loss | none at import |
| Human-readable | only if the original is also kept | original *is* the stored file |

Format notes:

- `.svs`, generic pyramidal TIFF, OME-TIFF, Philips TIFF, Ventana BIF — real tiled TIFF; JPEG
  tiles could be passed through (tile bytes + `JPEGTables`), JPEG 2000 (33003/33005) could not.
- `.ndpi` — TIFF-like, non-standard (single JPEG per level with restart markers, 64-bit offset
  hack). Needs OpenSlide or conversion.
- `.mrxs` — **not TIFF**, multi-file (`Slidedat.ini` + `Data*.dat`), same problem class as `.vsi`.
- **OpenSlide reads only local file paths** — no custom streams.
- `vips` reads svs/ndpi/mrxs directly via OpenSlide, so only VSI needs the slow `bfconvert` step.

Decision: all-DZI is the default. With a small team on low-bandwidth links, one serving path is
worth more than skipping conversion. Native passthrough stays an optional later optimisation — just
another tile-index type on the same range-read storage.

### 9.1 Sprint scope: external converter, efficient serving

**Server-side WSI conversion is not invested in this sprint.** The pipeline to make solid is:

```
VsiConverter (desktop, tools/VsiConverter)  →  {name}.dzi.zip (stored entries)
   → upload (browser or Drive upload folder)  →  DziImportPlugin: validate, build TileIndex,
     store the zip as uploaded (variant DziZip)  →  tiles served by range read (§8)
```

- **Import**: validate (exactly one `.dzi`, its `_files/` folder, all entries stored — repack only if
  deflated), normalise `\` separators, build the index from **tile entries only**, parse
  dimensions from the `.dzi`. No extract → rename → re-zip round trip; the uploaded zip is stored
  unchanged.
- **Slide preview (existing fallback, reused)**: `DziImportPlugin.CreateThumbnailAsync`
  (`DziImportPlugin.cs:181`) takes the highest level (≤ 12) that has exactly one tile and stores
  that tile's bytes as `ThumbData` — no decode, no vips. With the index this becomes an index
  lookup + one range read instead of a directory scan of the extracted `{id}_files/` — at import on
  the local zip, later (`ThumbnailWorker`, admin debug thumb) against any instance.
  - **Bug to fix with it**: it sets `ImageWidth = ImageHeight = ThumbSize` (`:208-209`), and import
    calls it (`:126`) *after* `ParseDziDimensions` (`:107`) — every imported slide ends up recorded
    as ThumbSize × ThumbSize. The thumbnail step must not touch the slide dimensions.
- **Converter tool**: keep as the WSI entry point. Small changes only: strip metadata at `dzsave`
  (`--keep none` / `--strip`), keep `NoCompression` and `/` separators, ZIP64 for large slides
  (.NET does this automatically).
- **Server conversion plugins** (`WsiConversionPlugin`, `VsiConversionPlugin`, bfconvert/vips on the
  server) stay as they are — no fixes, no extension. They only need to keep compiling against the
  new storage contract; if that is costly, disable them behind `WsiConversion:Enabled`.

## 10. Label, macro and metadata (privacy)

- `dzsave` writes only the main pyramid; OpenSlide associated images (label, macro, thumbnail)
  are only loaded when explicitly requested. `bfconvert -series N` converts one series; label and
  overview are other series. **Converting removes the label from what is served.**
- For VSI, the series chosen in the VsiConverter series dialog decides — a wrong pick converts the
  overview/label.
- `dzsave` writes **`vips-properties.xml` into `{id}_files/`** (confirmed in `docs/vsi/output_*`),
  and the tile endpoint serves any path under `_files/`. For the VSI → OME-TIFF route it carries
  only technical fields (size, resolution, tiling). **To verify:** for OpenSlide input (svs, ndpi)
  it likely carries vendor properties (`aperio.*`, `openslide.*` — original filename, user, date,
  possibly barcode).
  - Fix: the tile index contains **only tile entries** (`.webp`, `.jpeg`, `.jpg`, `.png` under
    `_files/`) plus the `.dzi` descriptor; the endpoint serves only what is in the index, so
    `vips-properties.xml` (and anything else in the zip) is never served — even from zips uploaded
    as-is. Additionally the converter tool strips metadata at `dzsave` (`--keep none` on
    vips ≥ 8.15, `--strip` before).
- Kept originals still contain the label (see §13).

## 11. Gotchas found while researching

- **Every file stored on Drive is made public ("anyone with the link, reader").**
  `CreateViewLink` (`GoogleDriveStorage.cs:469`) runs on every `PutFileAsync` (line 228) and on
  upload-folder imports of non-WSI files (line 852); `CreatePublicRangeLinkAsync` (line 645) runs on
  WSI imports (line 847) and stores `…/files/{id}?alt=media&key={PUBLIC_API_KEY}` in
  `NodeFile.PublicUrl`. (Line 603 is inside a commented-out block.) These links bypass iPath's access
  check. Their original purpose — serving Drive files straight to browsers — no longer holds (to
  confirm in review): `uc?export=` links are unreliable/embedding-blocked and large files get a
  virus-scan interstitial; `thumbnailLink` URLs are short-lived, so stored view links are likely dead;
  only the `alt=media&key=` URL still works, and it is exactly the privacy problem (plus API-key
  exposure and project quota). `PublicUrl` is only read by the admin storage info dialog.
  → Decision: remove `PublicUrl`, `CreatePublicRangeLinkAsync`, the "anyone" permission in
  `CreateViewLink`, and `GoogleDriveConfig.PUBLIC_API_KEY`; serve via the local cache (§5).
- `CacheManager.GetOrPrepareAsync` writes `LastAccessed`/`AccessCount` with a `SaveChangesAsync`
  on every hit. If it sits on the tile path, that is a DB write per tile — throttle the access
  bookkeeping (e.g. only when older than a minute) when moving it behind the decorator.

- `tools/VsiConverter/VsiConverter.UI/Services/ToolchainManager.cs:141` downloads
  `vips-dev-w64-web`, which is built **without OpenSlide**. Relying on vips for svs/ndpi/mrxs
  needs the "all" build — check the server's vips build too.
- `ZipArchive` is not thread-safe; if used for local reads, one instance per request (or use the
  own index).
- Cold-cache extraction today has no single-flight guard: two tile requests can extract the same
  zip concurrently.
- Older zips carry `\` separators in entry names; `ZipExtraction.ExtractNormalized` handles this —
  the index builder must normalise the same way.

## 12. Decisions (taken 2026-09-26)

1. **Topology**: `Storage:Default` plus an optional per-community instance; the group's **main
   community** (`Group.CommunityId`) decides. Files without a community are always on local server
   storage. All data of a community lives in one instance.
2. **Named instances** from configuration, loaded at startup; several Google Drives are possible.
   No switching in a running application.
3. **Every stored object records its location** (instance, key, variant, status
   `Pending | Active | Retired | Purged`).
4. **Migration is rare, manual and complete** — an admin tool in a maintenance window. Moving a
   group into, out of or between communities is a migration, not a DB update.
5. Storage primitive is the **range read**; local keeps zero-copy via `PhysicalFileRange`.
6. **Keys are written once and stored**; renames do not touch storage until the next migration.
7. **All data is served through the app** for authorization; no presigned URLs. Server caching is a
   per-instance decorator.
8. **All-DZI** is the default WSI format; tiles served from the stored zip via an import-time index.
9. User upload folder / import scanner are a **Drive-only capability**, not part of the general
   contract; a user is assigned **0 or 1 Drive instance** for uploads.
10. **`PublicUrl` and public Drive permissions are removed**; remote objects are served through
    the app from the existing local cache management.
11. **No backward compatibility** in this sprint — not in production; dev data is re-imported.
12. **Human-readable keys only on Google Drive**; S3 and LocalFiles use id-only keys. **S3 is a
    minimal blob provider**; S3 and LocalFiles share the key layout
    `{groupId}/{requestId}/{documentId}/{variant}` for per-group backup and operations.
13. **Originals are kept** (variant `Original` next to `DziZip`) for now.
14. **Every migration writes a local per-group backup**, purged only manually together with the
    retired source.
15. **A community is a tenant**: cases move only within their main community (cross-community move
    is warned and blocked); groups never leave their main community.
16. **Upload folders are shared with the Google account's email**; if the iPath email differs, a
    notice goes there.
17. **CaseRoom guests may download originals.**
18. **Legacy iPath2 documents stay as they are for now** (external links, testing only); their
    binaries are migrated later.
19. **WSI in this sprint = external VsiConverter + efficient serving.** Uploaded `.dzi.zip` is
    stored as-is and indexed; server-side conversion is left untouched (§9.1).

## 13. Open questions

- **Label in kept originals** — de-identify (blank label/macro at import) / keep intact and
  restrict storage access. *Decide later.*
- Whether to keep originals long-term (costs roughly the original's size again). *Kept for now,
  decide later.*

## 14. Next time — first steps (smallest first)

0. **Remove public Drive links** — drop `PublicUrl`, `CreatePublicRangeLinkAsync`, the "anyone"
   permission and `PUBLIC_API_KEY`; serve through the cache. The `NodeFile.PublicUrl` property
   itself stays unused until the step-7 migration, so step 0 needs no model migration. ✅
1. **DZI import as-is + tile index** — `DziImportPlugin` validates and indexes the uploaded
   `.dzi.zip` (tile entries only) and stores it unchanged; converter tool strips metadata.
2. **Range serving** from the stored zip for local storage; remove extraction to `TempDataPath`.
3. **Cache** access decision and tile index (`HybridCache`).
4. **Split the storage layers** (§4.1): `IStorageProvider` per instance, document storage service,
   instance registry from config. Local + current single Drive first.
5. **S3/RustFS provider** + RustFS container in the AppHost.
6. **Community instance override** + `List<StorageLocation>` (+ EF migration) + per-user upload
   Drive instance.
7. **Migration tool** + consistency check.
8. **Replay harness** + Toxiproxy profile; dev communities per instance.

## 15. Impact on application and UI

Surveyed 2026-09-26 on `feature/remote-storage-overhaul`. Grouped by area; each item is what must
change, not how.

### 15.1 Existing bugs this sprint must fix anyway (verified)

- **Moving a case to another group breaks local files.** `UpdateServiceRequestHandler.cs:106-119`
  only sets `node.GroupId`; `LocalStorageService.GetServiceRequestPath` (`:208`) builds the path
  from the *current* `GroupId` at read time. → stored keys (§6.2) fix it; cross-community moves
  become migrations.
- **`CacheManager` is not wired in**: `GetOrPrepareAsync` has no callers, so no
  `DocumentCacheEntry` rows exist and eviction never evicts anything; `ResolveDirectPath` returns
  null. Serving uses `GetDocumentFileHandler`'s own temp logic. "Reuse the cache management" (§5)
  therefore means *wire it in*, not just adapt it.
- **Upload worker dispatch** (`RemoteStorageUploadWorker.cs:37-41`): `DeleteServiceRequest` calls
  `DeleteFileAsync`; `DeleteDocument`/`FetchDocument` have no arm (runtime exception).
- **Upload queue is in-memory** (`RemoteStorageUploadQueue.cs`, bounded 100): queued uploads are
  lost on restart, and `CleanStaleCacheFilesHandler` may delete `TempDataPath/{id}` of a document
  whose upload never ran — the only copy. → `Pending` locations as a persistent outbox.
- **Unauthenticated `POST /test/upload`** (`TestEndpoints.cs:19`, mapped in `MapEndpoints.cs:29`)
  writes `TempDataPath/{id}-{file.FileName}` with a client-supplied name (path traversal). Remove or
  restrict to Development — independent of this sprint.
- **Community rename never reaches Drive**: `Community.UpdateName` raises `CommunityCreatedEvent`
  instead of `CommunityRenamedEvent`. And `ServiceRequestDescriptionUpdatedEvent` fires on every case
  edit, renaming the Drive folder synchronously inside `SaveChanges`.
- **Deletes never touch storage**: group destroy (`GroupService.cs:237-312`), sync-import delete and
  user delete hard-delete rows and leave files, Drive folders and sharing permissions behind.
- `WsiConversionPlugin` writes `{id}.dzi`/`{id}_files` to `TempDataPath` and never zips or stores
  them — the DZI exists only until cache cleanup (reported; the original is uploaded). **Not fixed in
  this sprint** (server conversion out of scope, §9.1).
- `ServerConfig.razor:56-57` uses `File.Exists` on directories — check icons never show.
- `MyProfile.razor:13` compares `ExternalStorageName == "Google"`, but the provider is
  `"GoogleDrive"` and the key is in no appsettings — the Drive box is effectively hidden.

### 15.2 Client-writable storage metadata

`NodeFile` (`UpdateDocumentHandler.cs:24`), `RequestDescription` (`UpdateServiceRequestDescription.cs:24`),
`GroupSettings` (`GroupService.cs:193,227`) and `CommunitySettings` (`UpdateCommunityHandler.cs:29`)
are sent to the client and **replaced wholesale** on save, including their `Storage` (Drive folder
ids). A stale UI can clear them; a WASM client can inject them.

→ `List<StorageLocation>` and all provider folder ids live **outside** client-editable DTOs and are
ignored on update; the UI gets a read-only projection.

### 15.3 User management

- Admin assigns **0/1 Drive instance** per user (`UserProfileAdmin.razor:29-32` is the natural
  place; `UserAdminViewModel` has no storage code yet).
- `UserUploadFolderButton.razor`, `MyProfile.razor:13-34`: condition becomes "user has an assigned
  Drive instance"; create/delete folder on that instance. API `users/{id}/uploadfolder`
  (`IApiClient.cs:103-107`) gets the instance implicitly from the user.
- `UserDto.UploadFolderId` (`FirstOrDefault`, no instance) → instance + folder.
- Rename the misnamed `CreateRequestUploadFolderCommand` (it is the *user* command).
- **User delete / Google login removal** must delete the upload folder and revoke sharing
  (`DeleteUserCommandHandler.cs:13`, `ExternalLogins.razor:111`).
- **Sharing email (decided: the Google account's email).** Today the folder is shared with
  `user.Email` (`GoogleDriveStorage.cs:696`). That matches the Google account only for users who
  registered via Google (`ExternalLogin.razor:148-150` prefills it); a user who linked Google later
  has a different address and the folder never appears in their Drive. Linking stores only provider
  + Google subject id, not the Google email.
  - Capture the Google email claim when the login is linked (`AddLoginAsync`, `ExternalLogin.razor:175`,
    `Manage/ExternalLogins.razor:130`) and share the folder with **that** address; the Drive
    invitation goes there.
  - Store it on the folder (`UserUploadFolder.SharedWithEmail`) — revoke on unlink / folder delete /
    user delete targets that permission. Show it in the profile ("shared with …").
  - If `user.Email` differs from the Google email, **also send a notice to `user.Email`** via the
    normal iPath mail path, telling the user the upload folder was shared with their Google account
    and to check that mailbox / Drive. The notice contains no link to the folder.

### 15.4 Group management

- `GroupAdminSettings.razor:17` main-community dropdown → **read-only + "migrate…" admin action**;
  server trigger is `GroupService.UpdateGroupAsync:230`.
- **Decided: a group never leaves its main community** once assigned (only moves between
  communities, as a migration). Today's `HasValue` guard already prevents setting it back to null —
  keep and enforce it server-side.
- Create group (`CreateGroupDialog.razor`, `GroupService.cs:180-200`) fixes the initial storage via
  the community — fine, but stop copying `Settings.Storage` from the client.
- Extra communities (`GroupAdminCommunities.razor`, `AssignGroupToCommunityAsync`) have **no storage
  effect** — keep it that way. Show the effective storage instance in group admin.
- Group destroy must delete stored objects (all locations) — see 15.1.

### 15.5 Community management

- New: show the community's storage instance; "change storage → migration" admin action
  (`CommunityAdminSettings.razor`, `CreateCommunityDialog.razor`). Instance chosen at create,
  read-only once the community has data.
- Rename: Drive folder follows only via the (fixed) `CommunityRenamedEvent`; S3/local unaffected.

### 15.6 Cases and documents

- **Move case** (`MoveRequestDialog.razor`, `ServiceRequestViewModel.cs:872-888`): lists all groups of
  the owner, any moderator can move. **Decided: a community is like a tenant — cases move only
  within their main community.** Target group in the same main community → allowed (stored key
  stays, no file move). Target group in another main community → **warn in the dialog and block on
  the server** (`UpdateServiceRequestHandler.cs:106-119`), independent of whether the instances
  happen to be equal. The dialog should list only groups of the same main community, or mark the
  others as not allowed.
- Upload / email import / admin VSI import (`UploadNodeFileHandler.cs`, `EmailImportService.cs:299`,
  `WsiImportCommandHandler.cs`) resolve the instance from the case's community and create a
  `Pending` location; temp and staging become cache only.
- "Import from Google" (`ServiceRequestHeader.razor:27-30`, `ServiceRequestViewModel.cs:1193-1245`)
  depends on the case owner's assigned Drive instance, not on `ExternalStorageName`.
- "Sync with external storage" (`ServiceRequestHeader.razor:115-120`,
  `SyncServiceRequestToStorageHandler.cs`) → "retry pending locations" or removed.
- Purge (`PurgeDocumentFilesHandler.cs`, `GetDeletedDocumentsWithFilesHandler.cs`) iterates all
  locations of all instances; today it silently skips files on a non-active provider.
- `DocumentStorageInfoDialog.razor` + `GetDocumentStorageInfoHandler`: replace provider/StorageId/
  RemotePath/**Public URL**/mismatch with the location list (instance, key, variant, status) and the
  expected instance.
- `DocumentGallery.razor:105` `wsiSrc` is dead code.

### 15.7 Serving, viewer, CaseRoom

- `DocumentEndpoints.cs:66-163` (`files/{*filepath}`): extraction block replaced by index + range
  read (§8). `documents/{id}/{filename}` (`:39-64`) gets range support. Stray
  `using Google.Apis.Drive.v3.Data` (`:2`).
- **Keep the URL shape** `/api/v1/documents/files/{id}.dzi` + `{id}_files/…`: it is hard-coded in
  `DocumentGallery.razor:36`, `SlideShowPage.razor:43`, `CaseRoomPage.razor.cs:438-446`, and parsed by
  `CaseRoomTokenAuthMiddleware.cs:62-91` (guest `?token=`). **Decided: CaseRoom guests may download
  originals** — extend the middleware to `documents/{id}/{filename}`.
- `ipath-viewer.js:53-59` loads GeoTIFFTileSource from `cdn.jsdelivr.net` for non-DZI URLs — dead
  with `PublicUrl` gone; remove.
- Thumbnails stay base64 in the DB; producers (`ThumbnailWorker.cs:87-95`, debug thumb) assume a
  local file → fetch through the cache.
- **Legacy iPath2 documents** have no stored binaries; preview/thumbnail/download link to
  `www.ipath-network.com` (`DocumentExtensions.cs:47-68`). **Decided: left as is for now**
  (testing only); the binaries are migrated in a later step, then these links go.

### 15.8 Drive import scanner and upload folders

- `GDriveImportScanner.cs:60` resolves the single storage → resolve the Drive instance per folder via
  the owner's upload folder.
- `ImportUploadFolderAsync` (`GoogleDriveStorage.cs:777-919`): move only when the case's instance is
  the same Drive, otherwise download + store; drop `PublicUrl`; thumbnail fetch uses an unauthenticated
  `new HttpClient()` on `thumbnailLink` → use the authenticated Drive client (IHttpClientFactory).
- Unused API: `ScanExternalDocuments`, `DeleteServiceRequestUploadFolder` (no UI caller).

### 15.9 Admin pages

- `ServerConfig.razor:23-47` shows one provider → list configured instances with status and root.
- `SystemStatus.razor`: purge covers all locations/variants; cache section reflects the wired-in
  `CacheManager`. Stale-cache API has no UI caller.
- New admin page (`AdminNavMenu.razor`): instances, per-community assignment, migration runs and
  backups.
- `Program.cs:266` `InitStorageAsync` initialises **all** configured instances.

### 15.10 Registration, config, infrastructure

- `APIServicesRegistration.cs:154-158` (either Google or Local) → instance registry, keyed DI.
- `GoogleServicesRegistration.cs`, `GoogleDriveConfig` → per-instance config; `PUBLIC_API_KEY`
  removed. `GoogleDriveStorageService` static root state (`:40-42`) and single credential go.
  Unescaped names in Drive queries (`CreateOrGetFolderAsync:324`) break on `'`.
- `GoogleProxyEndpoints.cs` is never mapped — delete (or keep as range-read reference).
- `LocalChacheService` registered but unused — delete.
- `CacheSettings.DziTileCacheMode`/`MemoryTileSliding` defined but unread — reuse for §8.1 or delete.
- `WsiConversionJob.OriginalStorageId` holds a staging *path* (dir from upload, file from Drive
  import — so `CleanStaleConversionStagingHandler` misses Drive staging); `ConvertedStorageId` never
  written. Staging-root logic duplicated in 5 places.
- `iPathClientConfig.ExternalStorageName` → removed; UI gets per-user/per-case flags from the server.
- `openapi.json` exposes `publicUrl` → Debug build after the API change.
- **Postgres and SqlServer migrations are stale** (last 2026-05-10); only Sqlite is current. The
  StorageLocation schema needs all three.
- Aspire AppHost: add a RustFS resource for dev.

### 15.11 Tests

No tests cover Local/Drive storage, the upload worker, `CacheManager`, purge/stale handlers,
`GetDocumentFileHandler`, `DocumentEndpoints` or `CaseRoomTokenAuthMiddleware`. Minimum for this
sprint: zip index + range read (unit), S3 provider against RustFS (Testcontainers), key/instance
resolution, migration resume.

### 15.12 Localization

Strings live in `iPath.Blazor.Server/Locales/{en,de,fr,it}.json`. Many storage strings are
hard-coded English (`UserUploadFolderButton`, `MyProfile`, "Import from Google", snackbars,
`DocumentStorageInfoDialog`, `SystemStatus`, `ServerConfig`). Localize when touching them; run the
translation-update skill before release.

## 16. Out of scope

- DICOM WSI.
- Replicating the same data to several storages in production.
- Client-side TIFF tile sources (GeoTIFFTileSource) — needs range requests on raw files through
  the old proxies and large IFD downloads before first paint.
- On-the-fly tiling with OpenSlide.
