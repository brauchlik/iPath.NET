# VSI Conversion Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an extensible conversion pipeline that imports proprietary slide formats (starting with .vsi), converts them to DZI tiles via bfconvert + libvips, and serves them through OpenSeadragon.

**Architecture:** Importers (GDrive, browser upload, local dialog) detect conversion-eligible files via `IConversionPlugin`, stage them with companion files in a dedicated conversion directory, then hand off to the plugin for processing through the existing `VsiConversionQueue` + `VsiConversionWorker`. The plugin produces DZI tiles, thumbnails, and updates document metadata.

**Tech Stack:** .NET 10, EF Core (Sqlite/Postgres), bfconvert (Bio-Formats), vips (libvips), OpenSeadragon 5.0.1

---

## File Structure

### New Files

| File | Purpose |
|------|---------|
| `src/core/iPath.Application/Contracts/IConversionPlugin.cs` | Plugin interface |
| `src/core/iPath.Application/Features/Conversion/VsiConversionPlugin.cs` | .vsi plugin implementation |
| `src/core/iPath.Application/Features/Conversion/ConversionJobContext.cs` | Context passed to plugin ProcessAsync |
| `src/core/iPath.Application/Features/Conversion/ConversionResult.cs` | Result DTO from plugin processing |

### Modified Files

| File | Change |
|------|--------|
| `src/core/iPath.Domain/Config/VsiConversionConfig.cs` | Add `StagingPath` (conversion staging dir) |
| `src/infrastructure/iPath.Google/Storage/GoogleDriveStorage.cs` | `ImportUploadFolderAsync` — use plugins for companion folder detection + download |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Documents/Commands/UploadNodeFileHandler.cs` | Use plugins for .vsi detection + staging |
| `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Documents/Commands/VsiImportCommandHandler.cs` | Use plugins for companion copy |
| `src/infrastructure/iPath.API/APIServicesRegistration.cs` | Register plugins, config |
| `src/ui/iPath.Blazor.Server/appsettings.json` | Add `StagingPath` |

### Unchanged Files

| File | Why |
|------|-----|
| `VsiConversionWorker.cs` | Already handles bfconvert + vips; plugin does prep, worker does conversion |
| `VsiConversionQueue.cs` | No changes needed |

---

### Task 1: IConversionPlugin Interface

**Files:**
- Create: `src/core/iPath.Application/Contracts/IConversionPlugin.cs`
- Create: `src/core/iPath.Application/Features/Conversion/ConversionJobContext.cs`
- Create: `src/core/iPath.Application/Features/Conversion/ConversionResult.cs`

- [ ] **Step 1: Create the interface and types**

```csharp
// src/core/iPath.Application/Contracts/IConversionPlugin.cs
namespace iPath.Application.Contracts;

public interface IConversionPlugin
{
    bool CanHandle(string extension);
    IReadOnlyList<string> GetRequiredCompanions(string fileName);
    Task<ConversionResult> ProcessAsync(ConversionJobContext context, CancellationToken ct);
}
```

```csharp
// src/core/iPath.Application/Features/Conversion/ConversionJobContext.cs
namespace iPath.Application.Features.Conversion;

public record ConversionJobContext(
    Guid DocumentId,
    string StagingPath,         // temp/conversion/{guid}/ — main file + companions are here
    string OriginalFilename,    // slide.vsi
    string FileExtension,       // .vsi
    DocumentNode Document       // the DocumentNode entity — plugin writes thumbnail/dimensions here
);
```

```csharp
// src/core/iPath.Application/Features/Conversion/ConversionResult.cs
namespace iPath.Application.Features.Conversion;

public record ConversionResult(bool Success, string? ErrorMessage = null)
{
    public static ConversionResult Ok() => new(true);
    public static ConversionResult Fail(string message) => new(false, message);
}
```

- [ ] **Step 2: Build to verify compilation**

```bash
dotnet build src/core/iPath.Application/iPath.Application.csproj
```

- [ ] **Step 3: Commit**

```bash
git add src/core/iPath.Application/Contracts/IConversionPlugin.cs src/core/iPath.Application/Features/Conversion/
git commit -m "feat: add IConversionPlugin interface and conversion context types"
```

---

### Task 2: VsiConversionPlugin Implementation

**Files:**
- Create: `src/core/iPath.Application/Features/Conversion/VsiConversionPlugin.cs`

- [ ] **Step 1: Implement the plugin**

```csharp
// src/core/iPath.Application/Features/Conversion/VsiConversionPlugin.cs
using System.Diagnostics;
using iPath.Application.Contracts;
using iPath.Domain.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.Application.Features.Conversion;

public class VsiConversionPlugin(
    IOptions<VsiConversionConfig> config,
    ILogger<VsiConversionPlugin> logger)
    : IConversionPlugin
{
    public bool CanHandle(string extension) =>
        string.Equals(extension, ".vsi", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetRequiredCompanions(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        return [$"_{baseName}_"];
    }

    public async Task<ConversionResult> ProcessAsync(ConversionJobContext ctx, CancellationToken ct)
    {
        var vsiPath = Path.Combine(ctx.StagingPath, ctx.OriginalFilename);
        var tiffPath = Path.Combine(ctx.StagingPath, $"{ctx.DocumentId}.ome.tiff");

        // Step 1: bfconvert → OME-TIFF
        logger.LogInformation("bfconvert starting for document {DocId}", ctx.DocumentId);
        var bfResult = await RunBfconvertAsync(vsiPath, tiffPath, ct);
        if (!bfResult.Success)
            return ConversionResult.Fail(bfResult.ErrorMessage!);

        // Step 2: vips dzsave → DZI tiles
        var dziOutput = Path.Combine(ctx.StagingPath, ctx.DocumentId.ToString());
        logger.LogInformation("vips dzsave starting for document {DocId}", ctx.DocumentId);
        var vipsResult = await RunVipsDzsaveAsync(tiffPath, dziOutput, ct);
        if (!vipsResult.Success)
            return ConversionResult.Fail(vipsResult.ErrorMessage!);

        // Step 3: Extract thumbnail from DZI (smallest level)
        await ExtractThumbnailAsync(dziOutput, ctx, ct);

        // Step 4: Cleanup intermediate TIFF
        try { File.Delete(tiffPath); } catch { }

        return ConversionResult.Ok();
    }

    private async Task ExtractThumbnailAsync(string dziOutput, ConversionJobContext ctx, CancellationToken ct)
    {
        var smallestLevel = Path.Combine($"{dziOutput}_files", "0", "0_0.webp");
        if (File.Exists(smallestLevel))
        {
            var bytes = await File.ReadAllBytesAsync(smallestLevel, ct);
            ctx.Document.File.ThumbData = Convert.ToBase64String(bytes);
        }

        // Optionally read DZI properties for dimensions
        var propsFile = Path.Combine($"{dziOutput}_files", "vips-properties.xml");
        // Parse width/height from XML — skipped for now, can add later
    }

    private async Task<(bool Success, string? ErrorMessage)> RunBfconvertAsync(
        string inputPath, string outputPath, CancellationToken ct)
    {
        var cfg = config.Value;
        var psi = new ProcessStartInfo
        {
            FileName = cfg.BfconvertPath,
            Arguments = $"-series {cfg.SeriesIndex} -compression JPEG \"{inputPath}\" \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["BF_MAX_MEM"] = cfg.JavaMaxMemory;

        using var proc = Process.Start(psi);
        if (proc == null) return (false, "Failed to start bfconvert");

        var tcs = new TaskCompletionSource<bool>();
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) => tcs.TrySetResult(true);
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) logger.LogWarning("bfconvert: {Data}", e.Data);
        };

        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromMinutes(cfg.MaxConversionMinutes));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linked.Token));
            if (!proc.HasExited) { proc.Kill(true); return (false, "bfconvert timed out"); }
            if (proc.ExitCode != 0) return (false, $"bfconvert exited with code {proc.ExitCode}");
            return (true, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (!proc.HasExited) proc.Kill(true);
            return (false, "bfconvert timed out");
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> RunVipsDzsaveAsync(
        string inputPath, string outputPath, CancellationToken ct)
    {
        var cfg = config.Value;
        var psi = new ProcessStartInfo
        {
            FileName = cfg.VipsPath,
            Arguments = $"dzsave \"{inputPath}\" \"{outputPath}\" --tile-size 254 --overlap 1 --suffix .webp[Q={cfg.WebpQuality}]",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return (false, "Failed to start vips");

        var tcs = new TaskCompletionSource<bool>();
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) => tcs.TrySetResult(true);
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) logger.LogWarning("vips: {Data}", e.Data);
        };

        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromMinutes(cfg.MaxConversionMinutes));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linked.Token));
            if (!proc.HasExited) { proc.Kill(true); return (false, "vips dzsave timed out"); }
            if (proc.ExitCode != 0) return (false, $"vips dzsave exited with code {proc.ExitCode}");
            return (true, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (!proc.HasExited) proc.Kill(true);
            return (false, "vips dzsave timed out");
        }
    }
}
```

- [ ] **Step 2: Build to verify compilation**

```bash
dotnet build src/core/iPath.Application/iPath.Application.csproj
```

- [ ] **Step 3: Commit**

```bash
git add src/core/iPath.Application/Features/Conversion/VsiConversionPlugin.cs
git commit -m "feat: add VsiConversionPlugin with bfconvert+vips pipeline"
```

---

### Task 3: VsiConversionConfig — Add StagingPath

**Files:**
- Modify: `src/core/iPath.Domain/Config/VsiConversionConfig.cs`

- [ ] **Step 1: Add StagingPath**

```csharp
public class VsiConversionConfig
{
    public const string ConfigName = "VsiConversion";

    public bool Enabled { get; set; }
    public string BfconvertPath { get; set; } = "bfconvert";
    public string JavaMaxMemory { get; set; } = "8g";
    public int MaxConversionMinutes { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public string? TempPath { get; set; }
    public int SeriesIndex { get; set; } = 7;
    public string VipsPath { get; set; } = "vips";
    public int WebpQuality { get; set; } = 80;
    public string StagingPath { get; set; } = "";  // ← NEW: temp/conversion root
}
```

- [ ] **Step 2: Update appsettings.json**

```json
"VsiConversion": {
    "Enabled": true,
    "BfconvertPath": "...",
    "VipsPath": "...",
    "StagingPath": "C:/Daten/ipath_sqlite/temp/conversion"
}
```

- [ ] **Step 3: Commit**

```bash
git add src/core/iPath.Domain/Config/VsiConversionConfig.cs src/ui/iPath.Blazor.Server/appsettings.json
git commit -m "feat: add StagingPath to VsiConversionConfig"
```

---

### Task 4: Refactor UploadNodeFileHandler — Use Plugin System

**Files:**
- Modify: `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/Documents/Commands/UploadNodeFileHandler.cs`

- [ ] **Step 1: Inject plugins and replace hardcoded .vsi check**

Replace the constructor to inject `IEnumerable<IConversionPlugin>` and replace the hardcoded `.vsi` extension check with plugin detection.

```csharp
public class UploadDocumentFileCommandHandler(
    iPathDbContext db,
    IOptions<iPathConfig> opts,
    IOptions<iPathClientConfig> clientOpts,
    IUserSession sess,
    IThumbImageService srvThumb,
    IMediator mediator,
    IRemoteStorageUploadQueue queue,
    IVsiConversionQueue vsiConversionQueue,
    IMimetypeService srvMime,
    IOptions<VsiConversionConfig> vsiConfig,
    IEnumerable<IConversionPlugin> conversionPlugins,
    ILogger<UploadDocumentFileCommandHandler> logger)
    : IRequestHandler<UploadDocumentCommand, Task<DocumentDto>>
```

Then replace the .vsi check block (lines ~123-134):

```csharp
// Before:
if (string.Equals(Path.GetExtension(request.filename), ".vsi", StringComparison.OrdinalIgnoreCase))
{
    db.Set<VsiConversionJob>().Add(new VsiConversionJob { ... });
    await vsiConversionQueue.EnqueueAsync(document.Id, ct);
}

// After:
var ext = Path.GetExtension(request.filename);
var plugin = conversionPlugins.FirstOrDefault(p => p.CanHandle(ext));

if (plugin != null)
{
    // Stage file in conversion directory with original filename
    var stagingPath = Path.Combine(vsiConfig.Value.StagingPath, document.Id.ToString());
    Directory.CreateDirectory(stagingPath);
    var stagedFile = Path.Combine(stagingPath, request.filename);
    File.Copy(fn, stagedFile, true);
    
    // Copy companion files from source (if available from browser upload)
    if (!string.IsNullOrEmpty(request.FilePath))
    {
        var sourceDir = Path.GetDirectoryName(request.FilePath)!;
        foreach (var companion in plugin.GetRequiredCompanions(request.filename))
        {
            var companionSrc = Path.Combine(sourceDir, companion);
            var companionDst = Path.Combine(stagingPath, companion);
            if (Directory.Exists(companionSrc))
                CopyDirectory(companionSrc, companionDst);
        }
    }
    
    document.DocumentType = "wsi";
    
    db.Set<VsiConversionJob>().Add(new VsiConversionJob
    {
        Id = Guid.CreateVersion7(),
        DocumentId = document.Id,
        OriginalStorageId = stagingPath
    });
    await vsiConversionQueue.EnqueueAsync(document.Id, ct);
}
```

Add helper method:
```csharp
private static void CopyDirectory(string sourceDir, string destDir)
{
    Directory.CreateDirectory(destDir);
    foreach (var file in Directory.GetFiles(sourceDir))
        File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
    foreach (var dir in Directory.GetDirectories(sourceDir))
        CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
}
```

- [ ] **Step 2: Build and verify**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

---

### Task 5: Refactor GDrive Import — Use Plugin System

**Files:**
- Modify: `src/infrastructure/iPath.Google/Storage/GoogleDriveStorage.cs`

- [ ] **Step 1: Inject plugins and handle conversion files**

In the `ImportUploadFolderAsync` method, after the existing `.vsi` check block (lines ~801-810), replace with plugin-based logic:

```csharp
var ext = System.IO.Path.GetExtension(item.Name);
var plugin = _conversionPlugins.FirstOrDefault(p => p.CanHandle(ext));

if (plugin != null)
{
    // Stage: download .vsi + companion folder to conversion staging dir
    var stagingPath = Path.Combine(_vsiConfig.StagingPath, newDoc.Id.ToString());
    Directory.CreateDirectory(stagingPath);
    
    // Download .vsi from GDrive
    var localVsiPath = Path.Combine(stagingPath, item.Name);
    await DownloadFileFromGDriveAsync(item.Id, localVsiPath, ct);
    
    // Download companion files/folders per plugin hint
    foreach (var companionName in plugin.GetRequiredCompanions(item.Name))
    {
        var companionFolderId = await FindFolderByNameAsync(folder.StorageId, companionName, ct);
        if (companionFolderId != null)
        {
            var companionLocalPath = Path.Combine(stagingPath, companionName);
            Directory.CreateDirectory(companionLocalPath);
            await DownloadFolderContentsAsync(companionFolderId, companionLocalPath, ct);
        }
    }
    
    // Enqueue conversion
    db.Set<VsiConversionJob>().Add(new VsiConversionJob
    {
        Id = Guid.CreateVersion7(),
        DocumentId = newDoc.Id,
        OriginalStorageId = localVsiPath
    });
    await vsiConversionQueue.EnqueueAsync(newDoc.Id, ct);
    
    // Move original .vsi + companion folder to SR folder on GDrive (archive)
    await MoveFileOnGDriveAsync(item.Id, srFolderId, ct);
    var companionGDriveId = await FindFolderByNameAsync(folder.StorageId, companionName, ct);
    if (companionGDriveId != null)
        await MoveFileOnGDriveAsync(companionGDriveId, srFolderId, ct);
}
```

Add helper methods to GoogleDriveStorageService or a new GDriveImportHelper:
```csharp
private async Task DownloadFileFromGDriveAsync(string fileId, string localPath, CancellationToken ct)
{
    using var stream = new FileStream(localPath, FileMode.Create, FileAccess.Write);
    await GDrive.Files.Get(fileId).DownloadAsync(stream, ct);
}

private async Task<string?> FindFolderByNameAsync(string parentId, string folderName, CancellationToken ct)
{
    var request = GDrive.Files.List();
    request.Q = $"'{parentId}' in parents and name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
    request.Fields = "files(id, name)";
    var result = await request.ExecuteAsync(ct);
    return result.Files?.FirstOrDefault()?.Id;
}

private async Task DownloadFolderContentsAsync(string folderId, string localPath, CancellationToken ct)
{
    var request = GDrive.Files.List();
    request.Q = $"'{folderId}' in parents and trashed = false";
    request.Fields = "files(id, name, mimeType)";
    var result = await request.ExecuteAsync(ct);
    
    if (result.Files != null)
    {
        foreach (var item in result.Files)
        {
            if (item.MimeType == "application/vnd.google-apps.folder")
            {
                var subPath = Path.Combine(localPath, item.Name);
                Directory.CreateDirectory(subPath);
                await DownloadFolderContentsAsync(item.Id, subPath, ct);
            }
            else
            {
                var filePath = Path.Combine(localPath, item.Name);
                await DownloadFileFromGDriveAsync(item.Id, filePath, ct);
            }
        }
    }
}
```

- [ ] **Step 2: Build and verify**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

---

### Task 6: Refactor VsiConversionWorker — Delegate to Plugin

**Files:**
- Modify: `src/infrastructure/iPath.API/Services/Storage/VsiConversionWorker.cs`

- [ ] **Step 1: Simplify worker to delegate to plugins**

Replace the complex `ProcessJobAsync` method with a simpler version that finds the right plugin and delegates:

```csharp
private async Task ProcessJobAsync(Guid docId, CancellationToken ct)
{
    using var scope = _sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
    var plugins = scope.ServiceProvider.GetRequiredService<IEnumerable<IConversionPlugin>>();

    var job = await db.Set<VsiConversionJob>()
        .Include(j => j.Document)
        .FirstOrDefaultAsync(j => j.DocumentId == docId, ct);

    if (job?.Document is null) { /* ... */ return; }

    var plugin = plugins.FirstOrDefault(p => p.CanHandle(
        Path.GetExtension(job.Document.File.Filename ?? "")));

    if (plugin is null) { /* ... */ return; }

    try
    {
        var stagingPath = job.OriginalStorageId ?? 
            Path.Combine(_config.StagingPath, docId.ToString());

        var ctx = new ConversionJobContext(
            DocumentId: docId,
            StagingPath: stagingPath,
            OriginalFilename: job.Document.File.Filename!,
            FileExtension: Path.GetExtension(job.Document.File.Filename!),
            Document: job.Document
        );

        job.Status = VsiConversionStatus.Converting;
        job.StartedOn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var result = await plugin.ProcessAsync(ctx, ct);

        if (result.Success)
        {
            job.Status = VsiConversionStatus.Completed;
            job.CompletedOn = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            throw new Exception(result.ErrorMessage);
        }
    }
    catch (Exception ex)
    {
        // ... existing error handling + retry logic ...
    }
}
```

Remove: `RunBfconvertAsync`, `RunVipsDzsaveAsync`, `CopyDirectory`, companion folder logic — all moved to `VsiConversionPlugin`.

- [ ] **Step 2: Build and verify**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

---

### Task 7: DI Registration

**Files:**
- Modify: `src/infrastructure/iPath.API/APIServicesRegistration.cs`

- [ ] **Step 1: Register plugins**

```csharp
// Conversion plugins
services.AddSingleton<IConversionPlugin, VsiConversionPlugin>();
```

- [ ] **Step 2: Build and verify**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

---

### Task 8: Build & Full Verification

- [ ] **Step 1: Full solution build**

```bash
dotnet build --warnaserror
```

- [ ] **Step 2: Verify with existing VSI import dialog test**

1. Start app
2. Open a service request
3. Click "VSI Import (Test)" in attach menu
4. Paste path to .vsi file
5. Verify completion in logs

- [ ] **Step 3: Final commit**

```bash
git add -A
git commit -m "feat: complete VSI conversion pipeline with plugin architecture"
```
