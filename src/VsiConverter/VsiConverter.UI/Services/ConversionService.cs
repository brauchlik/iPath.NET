using System.Collections.ObjectModel;
using VsiConverter.UI.Models;
using VsiConverter.UI.ViewModels;

namespace VsiConverter.UI.Services;

public class ConversionService
{
    private readonly PipelineRunner _runner = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _currentCts;

    public ObservableCollection<ConversionItemViewModel> Queue { get; } = new();

    public event Action? QueueChanged;

    public async Task EnqueueAsync(string filePath)
    {
        if (Queue.Any(i => i.FilePath == filePath))
            return;

        var fileInfo = new FileInfo(filePath);
        var item = new ConversionItemViewModel
        {
            FilePath = filePath,
            FileName = fileInfo.Name,
            FileSize = FormatSize(fileInfo.Length),
            Status = ConversionStatus.Queued
        };

        var baseName = Path.GetFileNameWithoutExtension(filePath);
        var companionDir = Path.Combine(Path.GetDirectoryName(filePath)!, $"_{baseName}_");
        item.CompanionStatus = Directory.Exists(companionDir) ? "Companion found" : "Companion missing";

        Queue.Add(item);
        QueueChanged?.Invoke();

        _ = ProcessQueueAsync();
    }

    public async Task EnqueueFolderAsync(string folderPath)
    {
        foreach (var file in Directory.GetFiles(folderPath, "*.vsi", SearchOption.AllDirectories))
        {
            await EnqueueAsync(file);
        }
    }

    public void CancelItem(ConversionItemViewModel item)
    {
        if (item.Status is ConversionStatus.Queued or ConversionStatus.Failed)
        {
            item.Status = ConversionStatus.Cancelled;
            item.StatusText = "Cancelled";
            QueueChanged?.Invoke();
        }
        else if (item.Status is ConversionStatus.Converting or ConversionStatus.CheckingCompanion or ConversionStatus.DetectingSeries)
        {
            _currentCts?.Cancel();
        }
    }

    public void CancelAll()
    {
        _currentCts?.Cancel();
        foreach (var item in Queue)
        {
            if (item.Status is ConversionStatus.Queued)
            {
                item.Status = ConversionStatus.Cancelled;
                item.StatusText = "Cancelled";
            }
        }
        QueueChanged?.Invoke();
    }

    public void ClearCompleted()
    {
        for (int i = Queue.Count - 1; i >= 0; i--)
        {
            if (Queue[i].Status is ConversionStatus.Completed or ConversionStatus.Cancelled)
                Queue.RemoveAt(i);
        }
        QueueChanged?.Invoke();
    }

    private async Task ProcessQueueAsync()
    {
        if (_gate.CurrentCount == 0) return;
        await _gate.WaitAsync();

        try
        {
            var next = Queue.FirstOrDefault(i => i.Status == ConversionStatus.Queued);
            if (next is null) return;

            _currentCts = new CancellationTokenSource();
            var token = _currentCts.Token;

            next.Status = ConversionStatus.Converting;
            next.StatusText = "Starting...";
            QueueChanged?.Invoke();

            var startTime = DateTime.UtcNow;
            var progress = new Progress<ConversionProgress>(p =>
            {
                next.Progress = p.Percent;
                if (p.Detail is not null)
                {
                    next.StatusText = p.Detail;
                    next.Log += (next.Log.Length > 0 ? "\n" : "") + p.Detail;
                }
                if (p.Stage == "Zipping DZI")
                    next.Status = ConversionStatus.Zipping;
                var elapsed = DateTime.UtcNow - startTime;
                next.ElapsedText = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            });

            var settings = SettingsStore.Load();
            var quality = settings.CompressionQuality;

            // Auto-detect best series
            var series = await SeriesDetector.DetectSeriesAsync(next.FilePath, token);
            var bestIndex = series.Count > 0 ? series.MaxBy(s => s.Width * s.Height)!.Index : 0;

            var result = await _runner.RunAsync(next.FilePath, bestIndex, quality, progress, token);

            if (result.Success)
            {
                next.Status = ConversionStatus.Completed;
                next.Progress = 100;
                next.StatusText = "Completed";
                next.OutputPath = result.OutputPath;
                if (result.OutputPath is not null)
                {
                    var outFile = new FileInfo(result.OutputPath);
                    if (outFile.Exists)
                        next.OutputSize = FormatSize(outFile.Length);
                }
            }
            else if (result.IsCancelled)
            {
                next.Status = ConversionStatus.Cancelled;
                next.StatusText = "Cancelled";
            }
            else
            {
                next.Status = ConversionStatus.Failed;
                next.ErrorText = result.ErrorMessage;
                next.StatusText = "Failed";
            }

            QueueChanged?.Invoke();
        }
        finally
        {
            _gate.Release();
        }

        // Process next item if any
        if (Queue.Any(i => i.Status == ConversionStatus.Queued))
            _ = ProcessQueueAsync();
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        int i;
        double size = bytes;
        for (i = 0; i < suffixes.Length - 1 && size >= 1024; i++)
            size /= 1024;
        return $"{size:F1} {suffixes[i]}";
    }
}
