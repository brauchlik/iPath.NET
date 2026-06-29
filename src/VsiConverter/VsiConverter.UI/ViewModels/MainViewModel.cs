using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using VsiConverter.UI.Models;
using VsiConverter.UI.Services;

namespace VsiConverter.UI.ViewModels;

public record SeriesSelectionResult(int SelectedIndex, bool UseForAll);

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ConversionService _conversionService;
    private bool _toolsReady;
    private bool _showToolsWarning;
    private int? _sessionSeriesIndex;
    private bool _useSameSeries;

    public MainViewModel(ConversionService conversionService)
    {
        _conversionService = conversionService;
        _conversionService.QueueChanged += OnQueueChanged;
    }

    public ObservableCollection<ConversionItemViewModel> Queue => _conversionService.Queue;

    public bool ToolsReady { get => _toolsReady; set => SetProperty(ref _toolsReady, value); }
    public bool ShowToolsWarning { get => _showToolsWarning; set => SetProperty(ref _showToolsWarning, value); }

    /// <summary>
    /// Set by MainWindow to open the series selection dialog.
    /// Takes file path and detected series list, returns the user's choice or null if skipped.
    /// </summary>
    public Func<string, List<AvailableSeries>, Task<SeriesSelectionResult?>>? ShowSeriesSelectionDialogAsync { get; set; }

    public async Task CheckToolsAsync()
    {
        var status = await ToolchainManager.DetectAllAsync();
        ToolsReady = status.JavaFound && status.BfconvertFound && status.VipsFound;
        ShowToolsWarning = !ToolsReady;
    }

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

    public async Task AddFilesAsync(string[] filePaths)
    {
        if (!ToolsReady) return;
        foreach (var path in filePaths)
        {
            var seriesIndex = await ResolveSeriesIndexAsync(path);
            await _conversionService.EnqueueAsync(path, seriesIndex);
        }
    }

    public async Task AddFolderAsync(string folderPath)
    {
        if (!ToolsReady) return;
        var files = Directory.GetFiles(folderPath, "*.vsi", SearchOption.AllDirectories);
        foreach (var path in files)
        {
            var seriesIndex = await ResolveSeriesIndexAsync(path);
            await _conversionService.EnqueueAsync(path, seriesIndex);
        }
    }

    private async Task<int> ResolveSeriesIndexAsync(string filePath)
    {
        if (_useSameSeries && _sessionSeriesIndex.HasValue)
            return _sessionSeriesIndex.Value;

        var series = await SeriesDetector.DetectSeriesAsync(filePath);

        if (ShowSeriesSelectionDialogAsync is not null)
        {
            var result = await ShowSeriesSelectionDialogAsync(filePath, series);
            if (result is not null)
            {
                _useSameSeries = result.UseForAll;
                _sessionSeriesIndex = result.SelectedIndex;
                return result.SelectedIndex;
            }
        }

        // Dialog skipped or not available — use best
        return series.Count > 0 ? series.MaxBy(s => (long)s.Width * s.Height)!.Index : 0;
    }

    public void ClearDone() => _conversionService.ClearCompleted();
    public void CancelAll() => _conversionService.CancelAll();
    public void CancelItem(ConversionItemViewModel item) => _conversionService.CancelItem(item);

    public async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!ToolsReady) return;
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            var items = e.DataTransfer.TryGetFiles();
            if (items is null) return;
            foreach (var item in items)
            {
                if (item is IStorageFolder folder)
                    await AddFolderAsync(folder.Path.LocalPath);
                else if (item is IStorageFile sf && sf.Name.EndsWith(".vsi", StringComparison.OrdinalIgnoreCase))
                    await AddFilesAsync([sf.Path.LocalPath]);
            }
        }
    }

    private void OnQueueChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatsText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Queue)));
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
