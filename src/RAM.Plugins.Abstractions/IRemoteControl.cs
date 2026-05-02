namespace RAM.Plugins.Abstractions;

public interface IRemoteControl
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
