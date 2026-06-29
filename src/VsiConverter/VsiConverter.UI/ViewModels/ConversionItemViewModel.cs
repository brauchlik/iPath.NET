using System.ComponentModel;
using System.Runtime.CompilerServices;
using VsiConverter.UI.Models;
using VsiConverter.UI.Services;

namespace VsiConverter.UI.ViewModels;

public class ConversionItemViewModel : INotifyPropertyChanged
{
    private string _fileName = "";
    private string _filePath = "";
    private string _fileSize = "";
    private string _companionStatus = "";
    private ConversionStatus _status;
    private int _progress;
    private string _statusText = "";
    private string _elapsedText = "";
    private string? _outputPath;
    private string? _outputSize;
    private string? _errorText;
    private string _log = "";

    public string FileName { get => _fileName; set => SetProperty(ref _fileName, value); }
    public string FilePath { get => _filePath; set => SetProperty(ref _filePath, value); }
    public string FileSize { get => _fileSize; set => SetProperty(ref _fileSize, value); }
    public string CompanionStatus { get => _companionStatus; set => SetProperty(ref _companionStatus, value); }
    public ConversionStatus Status { get => _status; set => SetProperty(ref _status, value); }
    public int Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public string ElapsedText { get => _elapsedText; set => SetProperty(ref _elapsedText, value); }
    public string? OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }
    public string? OutputSize { get => _outputSize; set => SetProperty(ref _outputSize, value); }
    public string? ErrorText { get => _errorText; set => SetProperty(ref _errorText, value); }
    public string Log { get => _log; set => SetProperty(ref _log, value); }

    public bool IsDone => Status is ConversionStatus.Completed or ConversionStatus.Failed or ConversionStatus.Cancelled;
    public bool IsFailed => Status == ConversionStatus.Failed;
    public bool IsConverting => Status == ConversionStatus.Converting;

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
