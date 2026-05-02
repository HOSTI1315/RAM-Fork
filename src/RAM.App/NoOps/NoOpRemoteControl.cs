using RAM.Plugins.Abstractions;

namespace RAM.App.NoOps;

internal sealed class NoOpRemoteControl : IRemoteControl
{
    public bool IsRunning => false;
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
}
