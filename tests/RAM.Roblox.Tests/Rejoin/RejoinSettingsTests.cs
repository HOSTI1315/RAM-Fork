using RAM.Core.Models;
using RAM.Roblox.Rejoin;

namespace RAM.Roblox.Tests.Rejoin;

public class RejoinSettingsTests
{
    [Fact]
    public void FromAppSettings_maps_seconds_to_TimeSpans()
    {
        var app = new AppSettings
        {
            RejoinCheckIntervalSeconds = 30,
            RejoinGracePeriodSeconds = 20,
            MemoryThresholdMb = 250,
            WindowTitleCheckIntervalSeconds = 7,
        };

        var s = RejoinSettings.FromAppSettings(app);

        Assert.Equal(TimeSpan.FromSeconds(30), s.PollInterval);
        Assert.Equal(TimeSpan.FromSeconds(20), s.GracePeriod);
        Assert.Equal(250, s.MemoryThresholdMb);
        Assert.Equal(TimeSpan.FromSeconds(7), s.WindowTitleCheckInterval);
    }

    [Fact]
    public void FromAppSettings_clamps_zero_or_negative_seconds_to_one()
    {
        var app = new AppSettings
        {
            RejoinCheckIntervalSeconds = 0,
            RejoinGracePeriodSeconds = -5,
            MemoryThresholdMb = -100,
            WindowTitleCheckIntervalSeconds = 0,
        };

        var s = RejoinSettings.FromAppSettings(app);

        Assert.Equal(TimeSpan.FromSeconds(1), s.PollInterval);
        Assert.Equal(TimeSpan.FromSeconds(1), s.GracePeriod);
        Assert.Equal(0, s.MemoryThresholdMb);
        Assert.Equal(TimeSpan.FromSeconds(1), s.WindowTitleCheckInterval);
    }

    [Fact]
    public void FromAppSettings_clamps_unreasonably_large_values()
    {
        var app = new AppSettings
        {
            RejoinCheckIntervalSeconds      = 999999,
            RejoinGracePeriodSeconds        = 999999,
            MemoryThresholdMb               = 999999,
            WindowTitleCheckIntervalSeconds = 999999,
        };

        var s = RejoinSettings.FromAppSettings(app);

        Assert.Equal(TimeSpan.FromSeconds(3600), s.PollInterval);
        Assert.Equal(TimeSpan.FromSeconds(3600), s.GracePeriod);
        Assert.Equal(16384, s.MemoryThresholdMb);
        Assert.Equal(TimeSpan.FromSeconds(3600), s.WindowTitleCheckInterval);
    }

    [Fact]
    public void Default_uses_AppSettings_defaults()
    {
        var s = RejoinSettings.Default;
        Assert.Equal(TimeSpan.FromSeconds(15), s.PollInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), s.GracePeriod);
        Assert.Equal(200, s.MemoryThresholdMb);
        Assert.Equal(TimeSpan.FromSeconds(5), s.WindowTitleCheckInterval);
    }
}
