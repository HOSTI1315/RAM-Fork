using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RAM.UI.Converters;

/// <summary>
/// True when value equals parameter (both parsed as int). Used for segmented-tab
/// RadioButton.IsChecked binding to a SelectedTabIndex int.
/// </summary>
public sealed class IntEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is int i ? i : -1;
        var p = parameter switch
        {
            int pi => pi,
            string s when int.TryParse(s, out var ps) => ps,
            _ => -2,
        };
        return v == p;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var p = parameter switch
        {
            int pi => pi,
            string s when int.TryParse(s, out var ps) => ps,
            _ => -1,
        };
        return value is true ? p : Binding.DoNothing;
    }
}

/// <summary>
/// Visible when value (int) equals parameter; otherwise Collapsed.
/// </summary>
public sealed class IntEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is int i ? i : -1;
        var p = parameter switch
        {
            int pi => pi,
            string s when int.TryParse(s, out var ps) => ps,
            _ => -2,
        };
        return v == p ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
