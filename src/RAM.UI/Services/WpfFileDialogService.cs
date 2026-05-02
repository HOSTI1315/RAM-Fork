using System.Windows;
using Microsoft.Win32;
using RAM.Plugins.Abstractions;

namespace RAM.UI.Services;

public sealed class WpfFileDialogService : IFileDialogService
{
    public Task<string?> OpenFileAsync(string title, string filter) =>
        InvokeOnUi(() =>
        {
            var dlg = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                Multiselect = false,
            };
            return dlg.ShowDialog(Application.Current.MainWindow) == true ? dlg.FileName : null;
        });

    public Task<string?> SaveFileAsync(string title, string filter, string suggestedFileName) =>
        InvokeOnUi(() =>
        {
            var dlg = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = suggestedFileName,
                AddExtension = true,
                OverwritePrompt = true,
            };
            return dlg.ShowDialog(Application.Current.MainWindow) == true ? dlg.FileName : null;
        });

    private static Task<string?> InvokeOnUi(Func<string?> action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
            return Task.FromResult(action());
        return dispatcher.InvokeAsync(action).Task;
    }
}
