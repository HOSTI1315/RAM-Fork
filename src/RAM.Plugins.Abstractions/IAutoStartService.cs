namespace RAM.Plugins.Abstractions;

public interface IAutoStartService
{
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
    Task SetEnabledAsync(bool enabled, CancellationToken ct = default);
}
