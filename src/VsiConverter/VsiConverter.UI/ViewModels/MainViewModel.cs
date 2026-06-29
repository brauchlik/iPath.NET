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
}
