using System.ComponentModel;
using System.Runtime.CompilerServices;
using VsiConverter.UI.Models;

namespace VsiConverter.UI.ViewModels;

public class SeriesSelectionViewModel : INotifyPropertyChanged
{
    private AvailableSeries? _selectedSeries;
    private bool _useForAll;

    public SeriesSelectionViewModel(string filePath, List<AvailableSeries> availableSeries)
    {
        FileName = Path.GetFileName(filePath);
        AvailableSeries = availableSeries;
        if (availableSeries.Count > 0)
            SelectedSeries = availableSeries.MaxBy(s => (long)s.Width * s.Height);
        StatusText = availableSeries.Count > 0
            ? $"Found {availableSeries.Count} series"
            : "No series detected";
    }

    public string FileName { get; }
    public List<AvailableSeries> AvailableSeries { get; }
    public string StatusText { get; }

    public AvailableSeries? SelectedSeries
    {
        get => _selectedSeries;
        set => SetProperty(ref _selectedSeries, value);
    }

    public bool UseForAll
    {
        get => _useForAll;
        set => SetProperty(ref _useForAll, value);
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
