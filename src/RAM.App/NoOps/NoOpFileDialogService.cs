using RAM.Plugins.Abstractions;

namespace RAM.App.NoOps;

internal sealed class NoOpFileDialogService : IFileDialogService
{
    public Task<string?> OpenFileAsync(string title, string filter) => Task.FromResult<string?>(null);
    public Task<string?> SaveFileAsync(string title, string filter, string suggestedFileName)
        => Task.FromResult<string?>(null);
}
