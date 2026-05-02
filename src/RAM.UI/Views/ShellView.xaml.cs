using System.Windows;

namespace RAM.UI.Views;

public partial class ShellView : Window
{
    public ShellView()
    {
        InitializeComponent();
        StateChanged += OnStateChanged;
    }

    /// <summary>
    /// Known WPF + <c>WindowChrome</c> + <c>WindowStyle="None"</c> bug: a maximized
    /// window covers the working area but Windows reserves ~7 px on each side for the
    /// (invisible) resize border, clipping our content off-screen. Compensate by
    /// adding inner margin to the root grid only while maximized.
    /// </summary>
    private void OnStateChanged(object? sender, System.EventArgs e)
    {
        if (RootGrid is null) return;
        RootGrid.Margin = WindowState == WindowState.Maximized
            ? new Thickness(7)
            : new Thickness(0);
    }
}
