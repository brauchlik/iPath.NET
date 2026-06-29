# Task 4: SeriesDetector

**Status:** DONE  
**Commit:** `898274d` - feat: add SeriesDetector for parsing showinf output  
**Build:** 0 warnings, 0 errors  

## Summary

Created `src/VsiConverter/VsiConverter.UI/Services/SeriesDetector.cs` — a static class that:
- Locates `bfconvert` via `ToolchainManager.FindTool`
- Runs `showinf -nopix -no-upgrade` via `java -cp`
- Uses concurrent stdout/stderr reads (fixing the deadlock from Task 3's `RunDetectionAsync`)
- Parses series headers (`Series #0`, `Pixels #1`, etc.) and dimensions (`Width=1920`, `Height=1080`)
- Extracts `PixelSizeX` for physical pixel size metadata
- Returns `List<AvailableSeries>` with index, dimensions, pixel size, and series name
- Cancellation: 60s timeout via linked `CancellationTokenSource`, kills process on timeout
- Source-generated regex via `[GeneratedRegex]` on `partial` methods

## Concerns

None.
