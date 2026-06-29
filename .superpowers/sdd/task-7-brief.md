## Task 7: ViewModels (MainViewModel + SettingsViewModel)

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/ViewModels/MainViewModel.cs`
- Create: `src/VsiConverter/VsiConverter.UI/ViewModels/SettingsViewModel.cs`

**Interfaces:**
- Consumes: Task 6 (ConversionService, ConversionItemViewModel), Task 3 (ToolchainManager, ToolchainStatus)
- Produces: MainViewModel (binds MainWindow), SettingsViewModel (binds SettingsDialog)

- [ ] **Step 1: Create MainViewModel.cs**
  File: `src/VsiConverter/VsiConverter.UI/ViewModels/MainViewModel.cs`

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VsiConverter.UI.Services;

namespace VsiConverter.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ConversionService _conversionService;

    public MainViewModel(ConversionService conversionService)
    {
        _conversionService = conversionService;
        _conversionService.QueueChanged += OnQueueChanged;
    }

    public ObservableCollection<ConversionItemViewModel> Queue => _conversionService.Queue;

    public string? StatsText
    {
        get
        {
            var total = Queue.Count;
            var converting = Queue.Count(i => i.Status == Models.ConversionStatus.Converting);
            var done = Queue.Count(i => i.Status == Models.ConversionStatus.Completed);
            return $"{total} file{(total != 1 ? "s" : "")} | {(converting > 0 ? $"{converting} converting | " : "")}{done} completed";
        }
    }

    public void AddFiles(string[] filePaths)
    {
        foreach (var path in filePaths)
            _ = _conversionService.EnqueueAsync(path);
    }

    public void AddFolder(string folderPath)
    {
        _ = _conversionService.EnqueueFolderAsync(folderPath);
    }

    public void ClearDone() => _conversionService.ClearCompleted();
    public void CancelAll() => _conversionService.CancelAll();
    public void CancelItem(ConversionItemViewModel item) => _conversionService.CancelItem(item);

    public void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files is null) return;
            foreach (var file in files)
            {
                if (file is IStorageFolder folder)
                    AddFolder(folder.Path.LocalPath);
                else if (file is IStorageFile sf && sf.Name.EndsWith(".vsi", StringComparison.OrdinalIgnoreCase))
                    AddFiles([sf.Path.LocalPath]);
            }
        }
    }

    private void OnQueueChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatsText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Queue)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

- [ ] **Step 2: Create SettingsViewModel.cs**
  File: `src/VsiConverter/VsiConverter.UI/ViewModels/SettingsViewModel.cs`

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VsiConverter.UI.Services;

namespace VsiConverter.UI.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private int _compressionQuality = 90;
    private bool _javaFound;
    private bool _bfconvertFound;
    private bool _vipsFound;
    private string? _javaVersion;
    private string? _bfconvertPath;
    private string? _vipsPath;
    private string _statusText = "";
    private bool _isChecking;

    public int CompressionQuality
    {
        get => _compressionQuality;
        set => SetProperty(ref _compressionQuality, Math.Clamp(value, 50, 100));
    }

    public bool JavaFound { get => _javaFound; set => SetProperty(ref _javaFound, value); }
    public bool BfconvertFound { get => _bfconvertFound; set => SetProperty(ref _bfconvertFound, value); }
    public bool VipsFound { get => _vipsFound; set => SetProperty(ref _vipsFound, value); }
    public string? JavaVersion { get => _javaVersion; set => SetProperty(ref _javaVersion, value); }
    public string? BfconvertPath { get => _bfconvertPath; set => SetProperty(ref _bfconvertPath, value); }
    public string? VipsPath { get => _vipsPath; set => SetProperty(ref _vipsPath, value); }
    public bool IsChecking { get => _isChecking; set => SetProperty(ref _isChecking, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public async Task CheckToolsAsync()
    {
        IsChecking = true;
        StatusText = "Checking...";
        try
        {
            var status = await ToolchainManager.DetectAllAsync();
            JavaFound = status.JavaFound;
            JavaVersion = status.JavaVersion;
            BfconvertFound = status.BfconvertFound;
            BfconvertPath = status.BfconvertPath;
            VipsFound = status.VipsFound;
            VipsPath = status.VipsPath;

            if (JavaFound && BfconvertFound && VipsFound)
                StatusText = "All tools found";
            else
                StatusText = "Some tools missing";
        }
        finally
        {
            IsChecking = false;
        }
    }

    public async Task DownloadToolAsync(string toolName)
    {
        StatusText = $"Downloading {toolName}...";
        try
        {
            await ToolchainManager.DownloadToolAsync(toolName, new Progress<double>(p => { }), CancellationToken.None);
            StatusText = $"{toolName} downloaded";
            await CheckToolsAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Download failed: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
```

- [ ] **Step 3: Verify build**
  Run: `dotnet build src/VsiConverter/VsiConverter.UI/VsiConverter.UI.csproj`
  Expected: Build succeeds with 0 warnings, 0 errors

- [ ] **Step 4: Commit**
  ```bash
  git add src/VsiConverter/
  git commit -m "feat: add MainViewModel and SettingsViewModel"
  ```
