using System.Globalization;
using System.Windows.Data;

namespace RAM.UI.Converters;

/// <summary>Returns the uppercase first character of the input string, or '?' if empty.</summary>
public sealed class InitialLetterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrEmpty(s))
            return char.ToUpperInvariant(s[0]).ToString();
        return "?";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
