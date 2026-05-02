using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RAM.Roblox.Launch;

namespace RAM.SmokeTests.Scenarios;

internal static class MultiLaunchSmokeTest
{
    public static async Task<string?> RunAsync()
    {
        // We don't launch a real Roblox client (no real cookies, no real install).
        // Instead we exercise the locking machinery directly with concurrent acquires
        // and verify serialization holds — which is what RobloxLauncher relies on.

        using var temp = new TempRoot("multi-launch");
        var lockPath = Path.Combine(temp.Path, "Cookies.dat");
        using var fileLock = new CookieFileLock(lockPath);
        using var mutexBypass = new SingletonMutexBypass(
            $"RAM_TEST_LAUNCH_{Guid.NewGuid():N}");

        const int parallelLaunches = 3;
        const int holdMs = 200;

        int inside = 0, maxConcurrent = 0;
        var enterTimes = new List<DateTimeOffset>();
        var enterLock = new object();

        var sw = Stopwatch.StartNew();
        var launches = Enumerable.Range(0, parallelLaunches).Select(i => Task.Run(async () =>
        {
            mutexBypass.Acquire();   // shared singleton in the real launcher
            using (await fileLock.AcquireAsync())
            {
                lock (enterLock) enterTimes.Add(DateTimeOffset.UtcNow);
                var c = Interlocked.Increment(ref inside);
                InterlockedMax(ref maxConcurrent, c);
                await Task.Delay(holdMs);
                Interlocked.Decrement(ref inside);
            }
        })).ToArray();

        await Task.WhenAll(launches);
        sw.Stop();

        if (maxConcurrent > 1)
            throw new InvalidOperationException(
                $"Cookie lock did not serialize: max concurrent inside = {maxConcurrent}");

        // Total elapsed should be ≥ N * holdMs (serial), with some scheduling slack.
        var minExpectedMs = parallelLaunches * holdMs * 0.9;
        if (sw.Elapsed.TotalMilliseconds < minExpectedMs)
            throw new InvalidOperationException(
                $"Total elapsed {sw.Elapsed.TotalMilliseconds:F0}ms < expected ≥ {minExpectedMs:F0}ms");

        // Verify enter timestamps are monotonically increasing by at least holdMs each
        for (int i = 1; i < enterTimes.Count; i++)
        {
            var gap = (enterTimes[i] - enterTimes[i - 1]).TotalMilliseconds;
            if (gap < holdMs * 0.8)
                throw new InvalidOperationException(
                    $"Sequential entries {i - 1}→{i} only {gap:F0}ms apart (expected ≥ {holdMs * 0.8:F0}ms)");
        }

        return $"3 parallel acquires serialized: total {sw.Elapsed.TotalMilliseconds:F0}ms, " +
               $"max concurrent inside = {maxConcurrent}, mutex bypass active = {mutexBypass.IsActive}";
    }

    private static void InterlockedMax(ref int location, int value)
    {
        int initial;
        do { initial = location; if (value <= initial) return; }
        while (Interlocked.CompareExchange(ref location, value, initial) != initial);
    }
}
