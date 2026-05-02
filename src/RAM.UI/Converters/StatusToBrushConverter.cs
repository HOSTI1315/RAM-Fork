using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RAM.Core.Models;

namespace RAM.UI.Converters;

/// <summary>
/// Maps <see cref="AccountStatus"/> or <see cref="PresenceType"/> to the corresponding
/// status brush from the active theme. Resolves brushes by resource key so theme swaps
/// are picked up automatically.
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            AccountStatus.InGame      => "StatusInGame",
            AccountStatus.NotInGame   => "StatusOnline",
            AccountStatus.Restarting  => "StatusPremium",
            AccountStatus.Error       => "StatusError",
            AccountStatus.Unknown     => "StatusOffline",
            PresenceType.InGame       => "StatusInGame",
            PresenceType.InStudio     => "StatusOnline",
            PresenceType.Online       => "StatusOnline",
            PresenceType.Offline      => "StatusOffline",
            PresenceType.Invisible    => "StatusOffline",
            "error"                   => "StatusError",
            _                         => "StatusOffline",
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
