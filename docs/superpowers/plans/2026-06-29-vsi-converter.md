# VSI → DZI Converter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a cross-platform Avalonia desktop app that converts Olympus `.vsi` whole-slide images to `.dzi.zip` archives.

**Architecture:** Single Avalonia UI project under `src/VsiConverter/`. Internal service classes handle toolchain detection, series discovery (via showinf), conversion pipeline (bfconvert → vips dzsave → zip), and queue management. No shared libraries with iPath.NET.

**Tech Stack:** .NET 10, Avalonia UI, System.Diagnostics.Process for external tool invocation.

## Global Constraints

- Target framework: `net10.0`
- UI: Avalonia with FluentTheme, compiled bindings enabled
- No shared libraries with iPath.NET — completely separate project
- Project root: `src/VsiConverter/`
- Windows single-file publish: `dotnet publish -r win-x64 --self-contained /p:PublishSingleFile=true`
- macOS self-contained publish: `dotnet publish -r osx-arm64 --self-contained`
- External deps: java (JRE 17+), bfconvert, vips — detected or auto-downloaded at runtime
- Default WEBP quality: 90
- Default tile size: 254, overlap: 1
- Conversion runs one file at a time (sequential queue)
- Output placed next to source `.vsi` as `{basename}.dzi.zip`

---

## File Structure

```
src/VsiConverter/
├── VsiConverter.sln
└── VsiConverter.UI/
    ├── VsiConverter.UI.csproj
    ├── Program.cs
    ├── App.axaml
    ├── App.axaml.cs
    ├── MainWindow.axaml
    ├── MainWindow.axaml.cs
    ├── Models/
    │   ├── AvailableSeries.cs
    │   └── ConversionStatus.cs
    ├── ViewModels/
    │   ├── MainViewModel.cs
    │   ├── ConversionItemViewModel.cs
    │   └── SettingsViewModel.cs
    ├── Services/
    │   ├── ToolchainManager.cs
    │   ├── SeriesDetector.cs
    │   ├── PipelineRunner.cs
    │   └── ConversionService.cs
    ├── Views/
    │   ├── SettingsDialog.axaml
    │   ├── SettingsDialog.axaml.cs
    │   ├── AboutDialog.axaml
    │   └── AboutDialog.axaml.cs
    └── Converters/
        └── StatusToColorConverter.cs
```

### Task N Template — follow this exact structure for every task

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/path/to/file.cs`
- Modify: `...`

**Interfaces:**
- Consumes: [what this task uses from earlier tasks — exact signatures]
- Produces: [what later tasks rely on — exact function names, parameter and return types]

- [ ] **Step: Write code**
- [ ] **Step: Verify build**
- [ ] **Step: Commit**

---

## Task 1: Project Scaffolding

**Files:**
- Create: `src/VsiConverter/VsiConverter.sln`
- Create: `src/VsiConverter/VsiConverter.UI/VsiConverter.UI.csproj`
- Create: `src/VsiConverter/VsiConverter.UI/Program.cs`
- Create: `src/VsiConverter/VsiConverter.UI/App.axaml`
- Create: `src/VsiConverter/VsiConverter.UI/App.axaml.cs`
- Create: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml`
- Create: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml.cs`

**Interfaces:**
- Consumes: nothing (first task)
- Produces: buildable Avalonia project with empty window titled "VSI → DZI Converter"

- [ ] **Step 1: Create VsiConverter.sln**
- [ ] **Step 2: Create VsiConverter.UI.csproj**
- [ ] **Step 3: Create Program.cs**
- [ ] **Step 4: Create App.axaml + App.axaml.cs**
- [ ] **Step 5: Create MainWindow.axaml + MainWindow.axaml.cs**
- [ ] **Step 6: Verify build succeeds**
- [ ] **Step 7: Commit**

---

## Task 2: Models

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/Models/AvailableSeries.cs`
- Create: `src/VsiConverter/VsiConverter.UI/Models/ConversionStatus.cs`

**Interfaces:**
- Produces: `AvailableSeries` record, `ConversionStatus` enum

- [ ] **Step 1: Create AvailableSeries.cs** — record with `Index`, `Width`, `Height`, `PixelSizeX`, `Description`
- [ ] **Step 2: Create ConversionStatus.cs** — enum with: `Queued`, `CheckingCompanion`, `DetectingSeries`, `Converting`, `Zipping`, `Completed`, `Failed`, `Cancelled`
- [ ] **Step 3: Verify build**
- [ ] **Step 4: Commit**

---

## Task 3: ToolchainManager

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/Services/ToolchainManager.cs`

**Interfaces:**
- Produces: `ToolchainManager` static class with:
  - `static Task<ToolchainStatus> DetectAllAsync()`
  - `static Task DownloadToolAsync(string toolName, IProgress<double>, CancellationToken)`
  - `static string FindTool(string name)`
  - `static string GetStorageDirectory()`

- [ ] **Step 1: Create ToolchainManager.cs**
  - `GetStorageDirectory()` returns `%LOCALAPPDATA%/VsiConverter/bin` (Windows) or `~/Library/Application Support/VsiConverter/bin` (macOS)
  - `DetectAllAsync()` runs `java -version`, `bfconvert -version`, `vips --version` via Process, returns status struct
  - `FindTool(name)` checks storage dir first, then PATH
  - `DownloadToolAsync("bfconvert", ...)` downloads bftools.zip from GitHub, extracts to storage dir
  - `DownloadToolAsync("vips", ...)` downloads libvips binary zip, extracts to storage dir
  - Progress reporting via HttpClient with ResponseHeadersRead
- [ ] **Step 2: Verify build**
- [ ] **Step 3: Commit**

---

## Task 4: SeriesDetector

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/Services/SeriesDetector.cs`

**Interfaces:**
- Produces: `static Task<List<AvailableSeries>> DetectSeriesAsync(string vsiPath, CancellationToken)`

- [ ] **Step 1: Create SeriesDetector.cs**
  - Runs `java -cp "bfconvert.jar" loci.formats.tools.ImageInfo -nopix -no-upgrade "{vsiPath}"`
  - Parses stdout for series headers and Width/Height dimensions
  - Returns list of AvailableSeries
  - 60-second timeout
- [ ] **Step 2: Verify build**
- [ ] **Step 3: Commit**

---

## Task 5: PipelineRunner

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/Services/PipelineRunner.cs`

**Interfaces:**
- Produces: `PipelineRunner` class with `Task<ConversionResult> RunAsync(path, seriesIndex, quality, progress, ct)`
  - `ConversionResult(bool Success, string? OutputPath, string? ErrorMessage)`
  - `ConversionProgress(string Stage, int Percent, string? Detail)`

- [ ] **Step 1: Create PipelineRunner.cs**
  - Check companion folder `_{basename}_` exists
  - Run `bfconvert -series {N} -compression JPEG "input.vsi" "output.ome.tiff"` with BF_MAX_MEM=8g, 30-min timeout
  - Run `vips dzsave "tiff" "output" --tile-size 254 --overlap 1 --suffix ".webp[Q={quality}]"` with 30-min timeout
  - Zip DZI output to `{sourceDir}/{basename}.dzi.zip` with store compression
  - Cleanup temp directory in finally block
  - Handle CancellationToken (OperationCanceledException) gracefully
- [ ] **Step 2: Verify build**
- [ ] **Step 3: Commit**

---

## Task 6: ConversionService

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/Services/ConversionService.cs`

**Interfaces:**
- Consumes: PipelineRunner, ConversionItemViewModel
- Produces: `ConversionService` class with:
  - `ObservableCollection<ConversionItemViewModel> Queue`
  - `Task EnqueueAsync(string filePath)`
  - `Task EnqueueFolderAsync(string folderPath)`
  - `void CancelItem(ConversionItemViewModel)`
  - `void CancelAll()`
  - `void ClearCompleted()`
  - `event Action? QueueChanged`

- [ ] **Step 1: Create ConversionService.cs**
  - Single-file processing via SemaphoreSlim(1,1)
  - Enqueue checks companion existence, adds to queue, triggers processing
  - ProcessQueueAsync picks first Queued item, runs PipelineRunner, updates item status
  - CancelItem: for queued items, set Cancelled; for converting, cancel CTS
  - CancelAll: cancel CTS + cancel all queued items
  - ClearCompleted: remove Completed/Cancelled from collection
  - FormatSize helper for file sizes
- [ ] **Step 2: Verify build**
- [ ] **Step 3: Commit**

---

## Task 7: ViewModels

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/ViewModels/MainViewModel.cs`
- Create: `src/VsiConverter/VsiConverter.UI/ViewModels/ConversionItemViewModel.cs`
- Create: `src/VsiConverter/VsiConverter.UI/ViewModels/SettingsViewModel.cs`

**Interfaces:**
- Produces: ViewModels that MainWindow and dialogs bind to

- [ ] **Step 1: Create ConversionItemViewModel.cs** — INotifyPropertyChanged with properties: FileName, FilePath, FileSize, CompanionStatus, Status (ConversionStatus), Progress (0-100), StatusText, ElapsedText, OutputPath, OutputSize, ErrorText, IsDone, IsFailed, IsConverting
- [ ] **Step 2: Create MainViewModel.cs** — wraps ConversionService, exposes Queue, StatsText, AddFiles/AddFolder, ClearDone, CancelAll, CancelItem, OnDrop handler
- [ ] **Step 3: Create SettingsViewModel.cs** — CompressionQuality (50-100, default 90), toolchain status, CheckToolsAsync, DownloadToolAsync
- [ ] **Step 4: Verify build**
- [ ] **Step 5: Commit**

---

## Task 8: Views

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/Converters/StatusToColorConverter.cs`
- Modify: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml` (full layout)
- Modify: `src/VsiConverter/VsiConverter.UI/MainWindow.axaml.cs` (wire ViewModel + events)
- Create: `src/VsiConverter/VsiConverter.UI/Views/SettingsDialog.axaml`
- Create: `src/VsiConverter/VsiConverter.UI/Views/SettingsDialog.axaml.cs`
- Create: `src/VsiConverter/VsiConverter.UI/Views/AboutDialog.axaml`
- Create: `src/VsiConverter/VsiConverter.UI/Views/AboutDialog.axaml.cs`

- [ ] **Step 1: Create StatusToColorConverter.cs** — maps ConversionStatus to SolidColorBrush
- [ ] **Step 2: Update MainWindow.axaml** — toolbar (Add Files, Add Folder, Clear Done, Cancel All, Settings), ListBox with DataTemplate for each item (name, size, progress bar, companion status, status text, elapsed), empty view hint, drag-drop support, status bar
- [ ] **Step 3: Update MainWindow.axaml.cs** — wire ViewModel, StorageProvider file pickers, drag-drop events
- [ ] **Step 4: Create Views/SettingsDialog.axaml + .cs** — compression slider, toolchain status, download buttons
- [ ] **Step 5: Create Views/AboutDialog.axaml + .cs** — version info, OK button
- [ ] **Step 6: Verify build**
- [ ] **Step 7: Commit**

---

## Task 9: Integration & Final Polish

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/Services/SettingsStore.cs`
- No new files unless needed

- [ ] **Step 1: Create SettingsStore.cs** — JSON persistence to `{AppData}/VsiConverter/settings.json` for AppSettings (CompressionQuality, tool paths)
- [ ] **Step 2: Verify final build — `dotnet build --warnaserror`**
- [ ] **Step 3: Commit**

---

## Self-Review Check

- [ ] Spec coverage: Every requirement from the design doc has a corresponding task
- [ ] No placeholders: All code blocks contain real implementation code
- [ ] Type consistency: All signatures match across tasks
