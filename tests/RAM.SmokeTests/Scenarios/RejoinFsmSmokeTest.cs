using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.Rejoin;

namespace RAM.SmokeTests.Scenarios;

/// <summary>
/// End-to-end FSM smoke: a real <see cref="RejoinManager"/> with a stubbed launcher receives
/// a launch event, then receives a synthetic disconnect. Verifies the state stream:
/// <c>Idle → Watching → GracePeriod → Rejoining → Watching</c>. Exercises the channel +
/// consumer + grace timer + launch task continuation, end-to-end, without real processes.
/// </summary>
internal static class RejoinFsmSmokeTest
{
    private sealed class StubLauncher : ILauncher
    {
        public int CallCount { get; private set; }
        public Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(LaunchResult.Ok(99999 + CallCount, $"TID-{CallCount}"));
        }
    }

    private sealed class StubPresence : IPresenceProvider
    {
        public Task<IReadOnlyDictionary<ulong, UserPresence>> GetPresenceAsync(
            IReadOnlyCollection<ulong> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<ulong, UserPresence>>(
                new Dictionary<ulong, UserPresence>());
    }

    public static async Task<string?> RunAsync()
    {
        // Short grace so the timer fires within the smoke time budget.
        var settings = Options.Create(new AppSettings
        {
            RejoinCheckIntervalSeconds = 1,
            RejoinGracePeriodSeconds = 1,
            MemoryThresholdMb = 0,            // disable memory killer
            WindowTitleCheckIntervalSeconds = 1,
        });

        var launcher = new StubLauncher();
        var presence = new StubPresence();
        await using var manager = new RejoinManager(
            settings, launcher, presence, NullLoggerFactory.Instance);

        var states = new List<RejoinWorkerState>();
        var account = new Account { UserId = 42, Username = "smoke", Cookie = "c" };
        var target = new LaunchTarget.Place(123UL);

        // Initial launch — manager spawns a worker and dispatches SessionStarted.
        manager.OnAccountLaunched(
            account,
            LaunchResult.Ok(11111, "TID-INIT"),
            target,
            workerStateChanged: s => { lock (states) states.Add(s); });

        await WaitFor(() => manager.Snapshot().TryGetValue(42, out var s) && s == RejoinWorkerState.Watching,
                      TimeSpan.FromSeconds(2),
                      "worker reached Watching after SessionStarted");

        // Reach into the worker's channel via a synthetic disconnect. We can do this
        // through the public IRejoinManager API by treating the manager as the worker
        // owner: we use reflection only as a smoke-shortcut, since the production code
        // path (FlogWatcher emitting events) isn't reachable without a real Roblox process.
        var workersField = typeof(RejoinManager).GetField("_workers",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (workersField is null) throw new InvalidOperationException("Could not find _workers field");
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<ulong, RejoinWorker>)workersField.GetValue(manager)!;
        if (!dict.TryGetValue(42, out var worker))
            throw new InvalidOperationException("Worker not found in manager");

        await worker.OnEventAsync(new RejoinEvent.FLogStateChanged(
            RAM.Roblox.Watchers.FlogWatcher.GameState.Disconnected));

        await WaitFor(() => manager.Snapshot()[42] == RejoinWorkerState.GracePeriod,
                      TimeSpan.FromSeconds(2),
                      "worker reached GracePeriod after disconnect");

        // Wait for grace timer (1s) → Rejoining → launch → Watching with new pid.
        await WaitFor(() => manager.Snapshot()[42] == RejoinWorkerState.Watching && launcher.CallCount >= 1,
                      TimeSpan.FromSeconds(6),
                      "worker recovered to Watching after auto-rejoin launch");

        // Stop and verify clean Idle.
        manager.OnAccountDisabled(42);
        await WaitFor(() => manager.Snapshot()[42] == RejoinWorkerState.Idle,
                      TimeSpan.FromSeconds(2),
                      "worker reached Idle after disable");

        // Snapshot state stream
        List<RejoinWorkerState> observed;
        lock (states) observed = states.ToList();
        var sequence = string.Join(" → ", observed);

        // Required transitions (ignoring duplicates that the channel may emit if memory loop
        // raised intermediary noise). We require all four ordered: Watching, GracePeriod,
        // Rejoining, then back to Watching, then Idle.
        if (!ContainsOrdered(observed,
                RejoinWorkerState.Watching,
                RejoinWorkerState.GracePeriod,
                RejoinWorkerState.Rejoining,
                RejoinWorkerState.Watching,
                RejoinWorkerState.Idle))
        {
            throw new InvalidOperationException(
                $"Expected ordered Watching → GracePeriod → Rejoining → Watching → Idle. Got: {sequence}");
        }

        return $"FSM transitions: {sequence}. Launcher invoked {launcher.CallCount}× (auto-rejoin).";
    }

    private static async Task WaitFor(Func<bool> condition, TimeSpan timeout, string label)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < timeout)
            await Task.Delay(50);
        if (!condition())
            throw new TimeoutException($"Timed out waiting for: {label} ({timeout.TotalSeconds:F1}s)");
    }

    private static bool ContainsOrdered<T>(IList<T> source, params T[] needle)
    {
        var cmp = EqualityComparer<T>.Default;
        var i = 0;
        foreach (var s in source)
        {
            if (i < needle.Length && cmp.Equals(s, needle[i])) i++;
            if (i == needle.Length) return true;
        }
        return false;
    }
}
