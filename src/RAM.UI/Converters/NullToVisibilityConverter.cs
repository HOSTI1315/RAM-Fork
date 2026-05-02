using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RAM.UI.Converters;

/// <summary>
/// Null → Collapsed, non-null → Visible. Pass <c>"invert"</c> as parameter to flip.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value is null || (value is string s && string.IsNullOrEmpty(s));
        var invert = parameter as string == "invert";
        return (isNull ^ invert) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
