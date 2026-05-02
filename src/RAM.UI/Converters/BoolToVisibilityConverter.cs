using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RAM.UI.Converters;

/// <summary>
/// True → Visible, False → Collapsed. Pass <c>"invert"</c> as parameter to flip.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is true;
        var invert = parameter as string == "invert";
        return (b ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is Visibility.Visible;
        var invert = parameter as string == "invert";
        return v ^ invert;
    }
}
