namespace RAM.Plugins.Abstractions;

/// <summary>
/// Blocking confirm / error dialogs. Window-overlay dialogs (Add Account, Launch, etc.)
/// don't go through this — they're routed via <c>ShellViewModel.ActiveDialog</c>.
/// </summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, ConfirmDialogOptions? options = null);
    Task ShowErrorAsync(string title, string message);
}

public sealed record ConfirmDialogOptions(
    string ConfirmText = "Confirm",
    string CancelText = "Cancel",
    bool Destructive = false);
