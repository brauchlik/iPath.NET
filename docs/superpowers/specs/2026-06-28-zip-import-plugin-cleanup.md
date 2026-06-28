# Zip Import Plugin Pipeline Cleanup

## Overview

Cleanup of the `IConversionPlugin` zip import pipeline to produce a canonical DZI zip format, remove duplicate logic, and fix inconsistencies between `VsiConversionPlugin` and `DziImportPlugin`.

## Current Architecture

Three entry points enqueue into `VsiConversionWorker`:

| Entry point | File:line | Flow |
|---|---|---|
| Browser upload | `UploadNodeFileHandler:144` | Plugin detection via `CanHandle(ext)` or `FindPluginInZipAsync` |
| GDrive import | `GoogleDriveStorage:886` | Plugin detection, downloads files + companions |
| Retry | `VsiConversionWorker:222` | Re-enqueues on failure |

The "Import VSI from Path" dialog (`VsiImportCommandHandler`) routes through `UploadNodeFileHandler` (same path as #1).

## WSI Goal State

Every WSI document, regardless of import path, produces:

**Temp cache (tile serving):**
```
TempDataPath/{documentId}.dzi
TempDataPath/{documentId}_files/
  {level}/{col}_{row}.webp
```

**Remote storage:**
```
TempDataPath/{documentId}.zip
```
Zip entries: `{documentId}.dzi` + `{documentId}_files/...`

**Cache miss restoration (future):**
When tiles are not in temp cache: pull `{id}.zip` from remote storage → unzip to temp → serve. Noted in code as `💡 FUTURE` comments.

---

## Changes

### 1. Canonical Zip Format

Both plugins must produce `temp/{id}.zip` with entries `{id}.dzi` + `{id}_files/...`.

| Plugin | Current behavior | Desired |
|--------|-----------------|---------|
| `VsiConversionPlugin` | Writes to `temp/{id}` (no extension) | Write to `temp/{id}.zip` |
| `DziImportPlugin` | Keeps original zip with original entry names | Re-zip with canonical entry names |

### 2. Filename Set to `.dzi`

After conversion, `document.File.Filename` must end in `.dzi` so the OSD viewer renders it as WSI.

| Plugin | Current | Desired |
|--------|---------|---------|
| `VsiConversionPlugin` | Stays `slide.vsi` | Change to `slide.dzi` |
| `DziImportPlugin` | Already changes (line 192) | No change |

### 3. Remove `FindPluginInZipAsync` Fallback

`UploadNodeFileHandler:177-185` scans individual zip entry extensions and matches via `CanHandle()`. This can match plugins whose `CanHandleZip` returns `false` (e.g. `.vsi` inside zip → `VsiConversionPlugin` matched, but it can't process zips). Remove the fallback: if no plugin's `CanHandleZip` returns true, import zip as generic file.

### 4. Record Plugin Type on `VsiConversionJob`

Add `PluginType string?` property to `VsiConversionJob`. `UploadNodeFileHandler` and `GoogleDriveStorage` write `activePlugin.GetType().Name` at job creation. `VsiConversionWorker` reads it instead of opening the zip to re-discover the plugin. Eliminates duplicate `ZipFile.OpenRead` + `CanHandleZip(archive)` scan.

### 5. Metadata Extraction from DZI (Low Priority)

`DziImportPlugin.ProcessAsync` should parse `Width`/`Height` from the `.dzi` XML and write to `document.File.ImageWidth`/`ImageHeight`.

---

## Files to Modify

| File | Change |
|------|--------|
| `DziImportPlugin.cs` | Re-zip with canonical names after restructure |
| `VsiConversionPlugin.cs` | `ZipDziOutput`: output to `{id}.zip`; set filename to `.dzi` |
| `VsiConversionWorker.cs` | Read `PluginType` from job instead of opening zip |
| `UploadNodeFileHandler.cs` | Write `PluginType` to job; remove lines 177-185 |
| `VsiConversionJob.cs` | Add `PluginType string?` |
| `GoogleDriveStorage.cs` | Write `PluginType` to job |

---

## Non-Goals (Not in Scope)

- Thumbnail fixes for non-square DZI images
- Cache miss pull-and-unzip (annotate with `💡 FUTURE` comments)
- Streaming zip decompression from remote (annotate with `💡 FUTURE` comments)
- File size / tile count guardrails
- Thumbnail result return value checking (`DziImportPlugin:103`)
