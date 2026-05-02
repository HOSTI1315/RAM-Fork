using System.Windows;
using System.Windows.Controls;
using RAM.App.ViewModels;

namespace RAM.UI.Views.Dialogs;

public partial class AddAccountDialogView : UserControl
{
    public AddAccountDialogView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// PasswordBox.Password is not a DependencyProperty (security-by-design — never
    /// stored in a binding source); push it manually to the VM. This is one of two
    /// authorized exceptions to the "no code-behind beyond InitializeComponent" rule.
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && DataContext is AddAccountViewModel vm)
            vm.Password = pb.Password;
    }

    /// <summary>
    /// Drag-and-drop is a UI-platform mechanic, not VM logic — there's no clean MVVM
    /// route without a behavior package (which we don't include). The handler defers
    /// all parsing to <see cref="AddAccountViewModel.LoadBulkFileAsync"/>.
    /// </summary>
    private async void OnFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0
            && DataContext is AddAccountViewModel vm)
        {
            await vm.LoadBulkFileAsync(files[0]);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }
}
