using System.Globalization;
using System.Windows.Data;

namespace RAM.UI.Converters;

/// <summary>Formats a long with thousands separators (e.g. 12480 → "12,480").</summary>
public sealed class NumberFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long l) return l == 0 ? "—" : l.ToString("N0", CultureInfo.InvariantCulture);
        if (value is int i)  return i == 0 ? "—" : i.ToString("N0", CultureInfo.InvariantCulture);
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
