using System.Windows;
using System.Windows.Controls;
using RAM.Core.Models;

namespace RAM.UI.Controls;

public partial class StatusDot : UserControl
{
    public StatusDot() => InitializeComponent();

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(
            nameof(Status), typeof(PresenceType), typeof(StatusDot),
            new PropertyMetadata(PresenceType.Offline));

    public PresenceType Status
    {
        get => (PresenceType)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}
