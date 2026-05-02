using System.Globalization;
using System.Windows.Data;
using RAM.Core.Models;

namespace RAM.UI.Converters;

/// <summary>Returns a human-readable label for a <see cref="PresenceType"/>.</summary>
public sealed class PresenceLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            PresenceType.InGame    => "In Experience",
            PresenceType.InStudio  => "Studio",
            PresenceType.Online    => "Online",
            PresenceType.Offline   => "Offline",
            PresenceType.Invisible => "Invisible",
            _                      => "Unknown",
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
