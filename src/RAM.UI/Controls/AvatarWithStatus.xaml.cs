using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAM.Core.Models;

namespace RAM.UI.Controls;

public partial class AvatarWithStatus : UserControl
{
    public AvatarWithStatus() => InitializeComponent();

    public static readonly DependencyProperty UsernameProperty =
        DependencyProperty.Register(nameof(Username), typeof(string), typeof(AvatarWithStatus),
            new PropertyMetadata(""));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(PresenceType), typeof(AvatarWithStatus),
            new PropertyMetadata(PresenceType.Offline));

    public static readonly DependencyProperty ShowStatusProperty =
        DependencyProperty.Register(nameof(ShowStatus), typeof(bool), typeof(AvatarWithStatus),
            new PropertyMetadata(true));

    public static readonly DependencyProperty HasPremiumProperty =
        DependencyProperty.Register(nameof(HasPremium), typeof(bool), typeof(AvatarWithStatus),
            new PropertyMetadata(false));

    public static readonly DependencyProperty SurfaceBrushProperty =
        DependencyProperty.Register(nameof(SurfaceBrush), typeof(Brush), typeof(AvatarWithStatus),
            new PropertyMetadata(Brushes.Transparent));

    public string Username      { get => (string)GetValue(UsernameProperty);    set => SetValue(UsernameProperty, value); }
    public PresenceType Status  { get => (PresenceType)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public bool ShowStatus      { get => (bool)GetValue(ShowStatusProperty);    set => SetValue(ShowStatusProperty, value); }
    public bool HasPremium      { get => (bool)GetValue(HasPremiumProperty);    set => SetValue(HasPremiumProperty, value); }
    public Brush SurfaceBrush   { get => (Brush)GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
}
