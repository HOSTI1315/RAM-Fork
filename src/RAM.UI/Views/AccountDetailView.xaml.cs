using System.Windows;
using System.Windows.Controls;
using RAM.App.ViewModels;

namespace RAM.UI.Views;

public partial class AccountDetailView : UserControl
{
    public AccountDetailView()
    {
        InitializeComponent();
        // Detail VM is recreated on every SelectedItem change (per ShellViewModel
        // ctor). PasswordBox.Password isn't a DependencyProperty, so we re-populate
        // it from the new VM on DataContextChanged. Without this, switching back to
        // a previously-edited account shows an empty password field.
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is AccountDetailViewModel vm)
            ProxyPasswordField.Password = vm.ProxyPassword;
    }

    /// <summary>
    /// PasswordBox.Password ↔ VM sync (PasswordBox is not bindable by Microsoft design).
    /// Authorized code-behind exception, same as in AddAccountDialogView.
    /// </summary>
    private void OnProxyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && DataContext is AccountDetailViewModel vm)
            vm.ProxyPassword = pb.Password;
    }
}
