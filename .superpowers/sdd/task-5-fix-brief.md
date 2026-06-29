Fix Task 5: Two critical runtime bugs in PipelineRunner.cs

## Bug 1: bfconvert invocation (line 60-67)

Wrong: calling `java -cp "{path}" loci.formats.tools.ImageConverter ...`
This assumes `_bfconvertPath` is a JAR file. But `bfconvert` is a native executable (batch file/script) that internally calls Java.

Fix: Call `bfconvert` directly as a native executable. Set `BF_MAX_MEM` via `ProcessStartInfo.EnvironmentVariables`.

```csharp
var bfResult = await RunProcessAsync(
    _bfconvertPath,
    $"-series {seriesIndex} -compression JPEG \"{vsiPath}\" \"{omeTiff}\"",
    new Dictionary<string, string> { ["BF_MAX_MEM"] = "8g" },
    TimeSpan.FromMinutes(30),
    progress,
    ct);
```

## Bug 2: vips dzsave output path (lines 88-95, 63)

Wrong: code looks for `{dziDir}/{baseName}.dzi` and `{dziDir}/{baseName}_files/`
But vips `dzsave "input" "output"` creates **siblings**: `output.dzi` and `output_files/` at the same level as `output`, not children.

Fix: Look for `{dziDir}.dzi` and `{dziDir}_files/` in the temp directory.

```csharp
var dziBase = Path.Combine(tempDir, baseName);

// vips dzsave creates: {dziBase}.dzi and {dziBase}_files/
var dziFile = dziBase + ".dzi";
var filesDir = dziBase + "_files";
```

Then zip using these paths:

```csharp
if (File.Exists(dziFile))
    zip.CreateEntryFromFile(dziFile, $"{baseName}.dzi", CompressionLevel.NoCompression);

if (Directory.Exists(filesDir))
{
    foreach (var file in Directory.GetFiles(filesDir, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(tempDir, file);
        zip.CreateEntryFromFile(file, relativePath, CompressionLevel.NoCompression);
    }
}
```

Apply both fixes. Verify build: `dotnet build src/VsiConverter/VsiConverter.UI/VsiConverter.UI.csproj`

Then commit with:
```bash
git add src/VsiConverter/
git commit -m "fix: correct bfconvert invocation and vips dzsave output path"
```
