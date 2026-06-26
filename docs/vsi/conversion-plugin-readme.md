# Conversion Plugin — Developer Guide

## Overview

The conversion plugin system allows iPath.NET to import proprietary file formats that can't be displayed directly in a browser. A plugin knows what companion files a format needs and how to convert it into DZI tiles (or other web-friendly formats) served via OpenSeadragon.

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│  IMPORTER (GDrive / Browser Upload / Local Dialog)        │
│                                                          │
│  1. Detects extension → asks plugins: CanHandle(ext)?     │
│  2. Creates staging dir: temp/conversion/{guid}/          │
│  3. Saves main file + asks plugin: GetRequiredCompanions()│
│  4. Acquires companion files (GDrive DL / local copy)     │
│  5. Creates DocumentNode + VsiConversionJob                │
└───────────────────────┬──────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────┐
│  VsiConversionQueue → VsiConversionWorker                 │
│                                                          │
│  1. Finds the right IConversionPlugin for the extension    │
│  2. Calls plugin.ProcessAsync(ctx, ct)                    │
│  3. Plugin handles: convert → thumbnail → metadata        │
│  4. Worker updates job status                             │
└──────────────────────────────────────────────────────────┘
```

## IConversionPlugin Interface

Located in `src/core/iPath.Application/Contracts/IConversionPlugin.cs`:

```csharp
public interface IConversionPlugin
{
    /// <summary>
    /// Does this plugin handle files with this extension?
    /// Called by importers to decide if a file needs conversion.
    /// </summary>
    bool CanHandle(string extension);

    /// <summary>
    /// What additional files or folders does this format need alongside the main file?
    /// Called by importers to acquire companion data before conversion.
    /// Returns names relative to the main file's location.
    /// Example: .vsi needs "_slidename_/" folder
    /// </summary>
    IReadOnlyList<string> GetRequiredCompanions(string fileName);

    /// <summary>
    /// Convert the staged files into web-viewable format (DZI tiles).
    /// Called by VsiConversionWorker from the conversion queue.
    /// The context provides the staging path and Document entity for metadata.
    /// </summary>
    Task<ConversionResult> ProcessAsync(ConversionJobContext context, CancellationToken ct);
}
```

## ConversionJobContext

```csharp
public record ConversionJobContext(
    Guid DocumentId,           // DocumentNode.Id
    string StagingPath,        // temp/conversion/{guid}/ — all files are here
    string OriginalFilename,   // e.g. "slide.vsi"
    string FileExtension,      // e.g. ".vsi"
    DocumentNode Document      // Entity — plugin writes thumbnail, dimensions here
);
```

## Creating a New Plugin

### Step 1: Implement IConversionPlugin

```csharp
public class MyFormatPlugin : IConversionPlugin
{
    public bool CanHandle(string extension) =>
        string.Equals(extension, ".myformat", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetRequiredCompanions(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        return [$"{baseName}_data/"];  // companion folder convention
    }

    public async Task<ConversionResult> ProcessAsync(ConversionJobContext ctx, CancellationToken ct)
    {
        var inputPath = Path.Combine(ctx.StagingPath, ctx.OriginalFilename);
        var tiffPath = Path.Combine(ctx.StagingPath, $"{ctx.DocumentId}.ome.tiff");
        var dziOutput = Path.Combine(ctx.StagingPath, ctx.DocumentId.ToString());

        // Step 1: convert proprietary format → OME-TIFF
        await RunConverterAsync(inputPath, tiffPath, ct);

        // Step 2: slice TIFF → DZI tiles
        await RunVipsAsync(tiffPath, dziOutput, ct);

        // Step 3: extract thumbnail
        await ExtractThumbnailAsync(dziOutput, ctx, ct);

        // Step 4: cleanup intermediate
        File.Delete(tiffPath);

        return ConversionResult.Ok();
    }

    private async Task ExtractThumbnailAsync(string dziOutput, ConversionJobContext ctx, CancellationToken ct)
    {
        var thumbPath = Path.Combine($"{dziOutput}_files", "0", "0_0.webp");
        if (File.Exists(thumbPath))
        {
            var bytes = await File.ReadAllBytesAsync(thumbPath, ct);
            ctx.Document.File.ThumbData = Convert.ToBase64String(bytes);
        }
    }
}
```

### Step 2: Register in DI

In `APIServicesRegistration.cs`:

```csharp
services.AddSingleton<IConversionPlugin, MyFormatPlugin>();
```

That's it. The plugin is automatically discovered by importers and the conversion worker via `IEnumerable<IConversionPlugin>`.

## Thumbnail Extraction

Plugins should extract a thumbnail and write it to the Document entity, following the same pattern as `ThumbImageService.UpdateNodeAsync()`:

```csharp
ctx.Document.File.ThumbData = Convert.ToBase64String(jpegBytes);
ctx.Document.File.ImageWidth = width;
ctx.Document.File.ImageHeight = height;
```

The easiest source: level 0 of the DZI tile pyramid (the most zoomed-out level, usually a single tile).

## Output Convention

The plugin produces:
```
temp/conversion/{guid}/
├── {original_filename}      ← main file (kept as archive)
├── {companion_folder}/      ← if any (kept as archive)
├── {guid}.dzi               ← DZI descriptor (served to OSD)
└── {guid}_files/            ← DZI tiles
    ├── 0/0_0.webp           ← level 0 (thumbnail source)
    ├── 1/0_0.webp
    └── ...
```

After conversion, the `.dzi` + `_files/` are the only files needed for viewing. The original files can be archived/moved.

## Dependency Rules

- Plugins live in `iPath.Application` (or `iPath.Application.Features.Conversion`)
- Plugins must NOT reference `iPath.Google`, `iPath.API`, or any infrastructure project
- If a plugin needs external tools (bfconvert, vips), configure paths via `IOptions<T>` — never hardcode
- Plugins use `Process.Start` for CLI tools — no native library bindings in the plugin

## Testing a Plugin

1. Add to DI registration
2. Enable `VsiConversion.Enabled: true` in appsettings
3. Upload/import a file via browser upload or GDrive
4. Check logs for `"VSI conversion started/completed"` messages
5. Verify `.dzi` + `_files/` appear in the staging directory
6. Open the slide in the OSD viewer
