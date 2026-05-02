using RAM.Plugins.Abstractions;

namespace RAM.App.NoOps;

/// <summary>
/// Default safe-no implementation. Used in tests and for headless scenarios where
/// no UI is available. Confirm always returns false (assume cancellation).
/// </summary>
internal sealed class NoOpDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, ConfirmDialogOptions? options = null)
        => Task.FromResult(false);

    public Task ShowErrorAsync(string title, string message)
        => Task.CompletedTask;
}
