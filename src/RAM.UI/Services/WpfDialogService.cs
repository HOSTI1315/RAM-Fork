using System.Windows;
using RAM.Plugins.Abstractions;
using RAM.UI.Dialogs;

namespace RAM.UI.Services;

/// <summary>
/// WPF implementation of <see cref="IDialogService"/>. Routes confirm / error prompts
/// through a modal <see cref="ConfirmDialog"/> window, owned by the main shell.
/// </summary>
public sealed class WpfDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, ConfirmDialogOptions? options = null)
    {
        var opts = options ?? new ConfirmDialogOptions();
        return InvokeOnUi(() =>
        {
            var dlg = new ConfirmDialog(title, message, opts, showCancel: true)
            {
                Owner = Application.Current.MainWindow,
            };
            return dlg.ShowDialog() == true;
        });
    }

    public Task ShowErrorAsync(string title, string message)
    {
        return InvokeOnUi<object?>(() =>
        {
            var dlg = new ConfirmDialog(
                title, message,
                new ConfirmDialogOptions(ConfirmText: "OK"),
                showCancel: false)
            {
                Owner = Application.Current.MainWindow,
            };
            dlg.ShowDialog();
            return null;
        });
    }

    private static Task<T> InvokeOnUi<T>(Func<T> action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
            return Task.FromResult(action());
        return dispatcher.InvokeAsync(action).Task;
    }
}
