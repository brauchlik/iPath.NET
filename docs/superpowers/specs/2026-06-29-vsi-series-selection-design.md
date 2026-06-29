# VSI Converter: Series Selection Dialog

## Goal
Let the user choose which image series to convert when adding a .vsi file, instead of always auto-selecting the highest-resolution series.

## UX Flow
1. User drops/picks a .vsi file (or first file from a folder)
2. App detects available series (async, ~1s, still responsive)
3. A dialog appears listing all series with their metadata (index, resolution, pixel size)
4. User selects one series
5. Optional checkbox "Use same series for all files in this session" — when checked, subsequent files skip the dialog and reuse the same selection
6. On "OK", the file enters the queue with the chosen series. On "Skip - use best", auto-selects the highest-resolution series
7. After restart, the session flag resets — dialog shows again on first file

## Files
### New
- `ViewModels/SeriesSelectionViewModel.cs` — INPC VM with AvailableSeries list, SelectedSeries, UseForAll flag, StatusText
- `Views/SeriesSelectionDialog.axaml` + `.cs` — dialog with radio-button list, checkbox, OK/Skip buttons; returns `bool?` via `ShowDialog<bool?>`

### Modified
- `ViewModels/MainViewModel.cs` — add `_sessionSeriesIndex`/`_useSameSeries` state, `Func<...>? ShowSeriesSelectionDialogAsync` delegate (set by MainWindow), modify `AddFiles`/`AddFolder`
- `ViewModels/ConversionItemViewModel.cs` — add `SeriesInfo` string property for display
- `Services/ConversionService.cs` — `EnqueueAsync(string, int seriesIndex)`, remove auto-detect from `ProcessQueueAsync`, remove `EnqueueFolderAsync`
- `MainWindow.axaml.cs` — on load, set `_vm.ShowSeriesSelectionDialogAsync` to open dialog and read result

## Dialog return contract
- `ShowDialog<bool?>` — `true` = OK clicked (read `SelectedSeries.Index` + `UseForAll` from VM), `false`/`null` = Skip
- MainViewModel reads the VM properties after the dialog closes
