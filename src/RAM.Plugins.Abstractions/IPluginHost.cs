namespace RAM.Plugins.Abstractions;

public interface IPluginHost
{
    IReadOnlyList<IPlugin> Plugins { get; }
    Task StartAllAsync(CancellationToken ct = default);
    Task StopAllAsync(CancellationToken ct = default);
}
