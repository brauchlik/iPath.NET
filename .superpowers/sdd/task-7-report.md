# Task 7 Report: ViewModels

## Status: DONE

**Commit:** `4b0a199` — feat: add MainViewModel and SettingsViewModel

**Build:** `dotnet build VsiConverter.UI.csproj --warnaserror` — 0 warnings, 0 errors

**Files created:**
- `src/VsiConverter/VsiConverter.UI/ViewModels/MainViewModel.cs`
- `src/VsiConverter/VsiConverter.UI/ViewModels/SettingsViewModel.cs`

**Notes:**
- Adapted drag-drop handler from the brief's `e.Data.Contains(DataFormats.Files)` / `e.Data.GetFiles()` to the correct Avalonia 12 API: `e.DataTransfer.Contains(DataFormat.File)` / `e.DataTransfer.TryGetFiles()`. The brief's code used a WPF-era pattern.
- `IStorageFolder`/`IStorageFile` are from `Avalonia.Platform.Storage`; `DataFormat.File` and `Contains`/`TryGetFiles` extensions are from `Avalonia.Input`.
- `SettingsViewModel` matched the brief exactly — no API changes needed.
