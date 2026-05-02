namespace RAM.Plugins.Abstractions;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
