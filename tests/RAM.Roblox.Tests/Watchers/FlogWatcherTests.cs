using Microsoft.Extensions.Logging.Abstractions;
using RAM.Roblox.Watchers;

namespace RAM.Roblox.Tests.Watchers;

public class FlogWatcherTests
{
    [Fact]
    public async Task Detects_disconnect_when_FLog_emits_disconnect_marker()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ram-flog-{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllTextAsync(path, "boot line\n");
            using var watcher = new FlogWatcher(
                path,
                NullLogger<FlogWatcher>.Instance,
                pollInterval: TimeSpan.FromMilliseconds(50));

            FlogWatcher.GameState? observed = null;
            watcher.StateChanged += (_, s) => observed = s;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            _ = watcher.StartAsync(cts.Token);
            await Task.Delay(200, cts.Token);

            await File.AppendAllTextAsync(path,
                "[FLog::DataModel] disconnect requested by client\n", cts.Token);

            // Poll briefly for state change
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (observed != FlogWatcher.GameState.Disconnected && sw.Elapsed < TimeSpan.FromSeconds(3))
                await Task.Delay(50, cts.Token);

            Assert.Equal(FlogWatcher.GameState.Disconnected, observed);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Detects_beta_menu_marker()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ram-flog-{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllTextAsync(path, "");
            using var watcher = new FlogWatcher(
                path, NullLogger<FlogWatcher>.Instance, pollInterval: TimeSpan.FromMilliseconds(50));
            FlogWatcher.GameState? observed = null;
            watcher.StateChanged += (_, s) => observed = s;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            _ = watcher.StartAsync(cts.Token);
            await Task.Delay(200, cts.Token);

            await File.AppendAllTextAsync(path, "[FLog::SingleSurfaceApp] beta menu opened\n", cts.Token);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (observed != FlogWatcher.GameState.BetaMenu && sw.Elapsed < TimeSpan.FromSeconds(3))
                await Task.Delay(50, cts.Token);

            Assert.Equal(FlogWatcher.GameState.BetaMenu, observed);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Default_log_directories_include_roblox_and_bloxstrap()
    {
        Assert.Contains(FlogWatcher.DefaultLogDirectories, d => d.EndsWith(@"Roblox\logs"));
        Assert.Contains(FlogWatcher.DefaultLogDirectories, d => d.EndsWith(@"Bloxstrap\Logs"));
    }
}
