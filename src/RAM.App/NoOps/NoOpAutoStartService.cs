using RAM.Plugins.Abstractions;

namespace RAM.App.NoOps;

internal sealed class NoOpAutoStartService : IAutoStartService
{
    public Task<bool> IsEnabledAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task SetEnabledAsync(bool enabled, CancellationToken ct = default) => Task.CompletedTask;
}
