using System.Windows;
using RAM.Plugins.Abstractions;

namespace RAM.UI.Dialogs;

public partial class ConfirmDialog : Window
{
    public static readonly DependencyProperty IsDestructiveProperty =
        DependencyProperty.Register(nameof(IsDestructive), typeof(bool), typeof(ConfirmDialog),
            new PropertyMetadata(false));

    public bool IsDestructive
    {
        get => (bool)GetValue(IsDestructiveProperty);
        set => SetValue(IsDestructiveProperty, value);
    }

    public bool ShowCancel { get; set; } = true;

    public ConfirmDialog(string title, string message, ConfirmDialogOptions options, bool showCancel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmText.Text = options.ConfirmText;
        CancelButton.Content = options.CancelText;
        IsDestructive = options.Destructive;
        ShowCancel = showCancel;
        if (!showCancel)
            CancelButton.Visibility = Visibility.Collapsed;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
