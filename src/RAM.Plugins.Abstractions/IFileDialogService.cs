namespace RAM.Plugins.Abstractions;

/// <summary>
/// Native file open / save dialogs. WPF impl wraps <c>Microsoft.Win32.OpenFileDialog</c>
/// and <c>SaveFileDialog</c>; tests / headless host get a no-op that returns null.
/// </summary>
public interface IFileDialogService
{
    Task<string?> OpenFileAsync(string title, string filter);
    Task<string?> SaveFileAsync(string title, string filter, string suggestedFileName);
}
