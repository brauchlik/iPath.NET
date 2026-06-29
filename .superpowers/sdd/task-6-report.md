# Task 6 Report: ConversionService + ConversionItemViewModel

- **Status:** DONE
- **Commit:** `dcd4e38` — feat: add ConversionItemViewModel and ConversionService
- **Build:** 0 warnings, 0 errors
- **Concerns:** None
- **Files created:**
  - `src/VsiConverter/VsiConverter.UI/ViewModels/ConversionItemViewModel.cs` — INotifyPropertyChanged with all queue item properties
  - `src/VsiConverter/VsiConverter.UI/Services/ConversionService.cs` — queue orchestrator with SemaphoreSlim(1,1), ObservableCollection, cancellation
