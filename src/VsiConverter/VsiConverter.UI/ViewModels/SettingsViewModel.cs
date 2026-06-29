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
