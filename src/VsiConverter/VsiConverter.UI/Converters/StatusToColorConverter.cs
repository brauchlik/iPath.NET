using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VsiConverter.UI.Models;

namespace VsiConverter.UI.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Completed => new SolidColorBrush(Colors.Green),
                ConversionStatus.Failed => new SolidColorBrush(Colors.Red),
                ConversionStatus.Cancelled => new SolidColorBrush(Colors.Gray),
                ConversionStatus.Converting => new SolidColorBrush(Colors.DodgerBlue),
                _ => new SolidColorBrush(Colors.Gray),
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
