using RAM.Plugins.Abstractions;

namespace RAM.Storage.Tests.ViewModels;

/// <summary>Test dialog service that returns a configurable confirm result.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public int ConfirmCalls { get; private set; }
    public string? LastConfirmTitle { get; private set; }
    public string? LastConfirmMessage { get; private set; }
    public ConfirmDialogOptions? LastConfirmOptions { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message, ConfirmDialogOptions? options = null)
    {
        ConfirmCalls++;
        LastConfirmTitle = title;
        LastConfirmMessage = message;
        LastConfirmOptions = options;
        return Task.FromResult(ConfirmResult);
    }

    public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
}
