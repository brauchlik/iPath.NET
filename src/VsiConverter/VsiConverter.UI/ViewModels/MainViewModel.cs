using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using VsiConverter.UI.Services;

namespace VsiConverter.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ConversionService _conversionService;
    private bool _toolsReady;
    private bool _showToolsWarning;

    public MainViewModel(ConversionService conversionService)
    {
        _conversionService = conversionService;
        _conversionService.QueueChanged += OnQueueChanged;
    }

    public ObservableCollection<ConversionItemViewModel> Queue => _conversionService.Queue;

    public bool ToolsReady { get => _toolsReady; set => SetProperty(ref _toolsReady, value); }
    public bool ShowToolsWarning { get => _showToolsWarning; set => SetProperty(ref _showToolsWarning, value); }

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

    public void AddFiles(string[] filePaths)
    {
        if (!ToolsReady) return;
        foreach (var path in filePaths)
            _ = _conversionService.EnqueueAsync(path);
    }

    public void AddFolder(string folderPath)
    {
        if (!ToolsReady) return;
        _ = _conversionService.EnqueueFolderAsync(folderPath);
    }

    public void ClearDone() => _conversionService.ClearCompleted();
    public void CancelAll() => _conversionService.CancelAll();
    public void CancelItem(ConversionItemViewModel item) => _conversionService.CancelItem(item);

    public void OnDrop(object? sender, DragEventArgs e)
    {
        if (!ToolsReady) return;
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            var items = e.DataTransfer.TryGetFiles();
            if (items is null) return;
            foreach (var item in items)
            {
                if (item is IStorageFolder folder)
                    AddFolder(folder.Path.LocalPath);
                else if (item is IStorageFile sf && sf.Name.EndsWith(".vsi", StringComparison.OrdinalIgnoreCase))
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

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
