using RAM.Plugins.Abstractions;

namespace RAM.App.NoOps;

internal sealed class NoOpPluginHost : IPluginHost
{
    public IReadOnlyList<IPlugin> Plugins => Array.Empty<IPlugin>();
    public Task StartAllAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAllAsync(CancellationToken ct = default) => Task.CompletedTask;
}
