using Microsoft.Extensions.Logging.Abstractions;
using RAM.Roblox.Watchers;

namespace RAM.SmokeTests.Scenarios;

internal static class FlogWatcherSmokeTest
{
    public static async Task<string?> RunAsync()
    {
        using var temp = new TempRoot("flog");
        var logPath = Path.Combine(temp.Path, "0.0.0.0_yyyymmdd_player.log");
        await File.WriteAllTextAsync(logPath, "[FLog::SingleSurfaceApp] launching\n");

        using var watcher = new FlogWatcher(
            logPath,
            NullLogger<FlogWatcher>.Instance,
            pollInterval: TimeSpan.FromMilliseconds(100));

        var observed = new List<FlogWatcher.GameState>();
        watcher.StateChanged += (_, s) => observed.Add(s);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        _ = watcher.StartAsync(cts.Token);
        await Task.Delay(300, cts.Token);

        await File.AppendAllTextAsync(logPath,
            "[FLog::DataModel] DataModel created and loaded\n", cts.Token);
        await Task.Delay(500, cts.Token);

        await File.AppendAllTextAsync(logPath,
            "[FLog::DataModel] disconnect requested by client\n", cts.Token);

        // Wait up to 3s for Disconnected to surface
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (watcher.State != FlogWatcher.GameState.Disconnected && sw.Elapsed < TimeSpan.FromSeconds(3))
            await Task.Delay(50, cts.Token);

        if (watcher.State != FlogWatcher.GameState.Disconnected)
            throw new InvalidOperationException(
                $"Expected Disconnected, last state was {watcher.State}. Observed sequence: {string.Join(",", observed)}");

        var detectedAfterMs = sw.Elapsed.TotalMilliseconds;
        if (detectedAfterMs > 2500)
            throw new InvalidOperationException(
                $"Disconnect detection took {detectedAfterMs:F0}ms (expected ≤ 2500ms)");

        return $"Disconnect detected in {detectedAfterMs:F0}ms. " +
               $"Observed transitions: {string.Join(" → ", observed)}";
    }
}
