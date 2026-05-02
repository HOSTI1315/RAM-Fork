using RAM.Core.Models;

namespace RAM.Roblox.Rejoin;

/// <summary>
/// Snapshot of the rejoin-relevant fields from <see cref="AppSettings"/>, taken at
/// worker construction. Hot-reload is NOT supported in v1 — existing workers keep
/// their original snapshot until next launch (which constructs a fresh worker).
/// </summary>
public sealed record RejoinSettings(
    TimeSpan PollInterval,
    TimeSpan GracePeriod,
    int MemoryThresholdMb,
    TimeSpan WindowTitleCheckInterval)
{
    public static RejoinSettings FromAppSettings(AppSettings s) => new(
        PollInterval:             TimeSpan.FromSeconds(Math.Clamp(s.RejoinCheckIntervalSeconds,    1, 3600)),
        GracePeriod:              TimeSpan.FromSeconds(Math.Clamp(s.RejoinGracePeriodSeconds,      1, 3600)),
        MemoryThresholdMb:        Math.Clamp(s.MemoryThresholdMb, 0, 16384),
        WindowTitleCheckInterval: TimeSpan.FromSeconds(Math.Clamp(s.WindowTitleCheckIntervalSeconds, 1, 3600)));

    public static readonly RejoinSettings Default = FromAppSettings(new AppSettings());
}
