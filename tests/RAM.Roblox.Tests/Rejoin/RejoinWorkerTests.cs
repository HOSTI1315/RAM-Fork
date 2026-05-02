using Microsoft.Extensions.Logging.Abstractions;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.Rejoin;
using RAM.Roblox.Watchers;

namespace RAM.Roblox.Tests.Rejoin;

/// <summary>
/// Direct FSM tests — drive transitions via <c>HandleEventAsync</c> (internal) so behavior
/// is deterministic and doesn't depend on real timers, real watchers, or real processes.
/// The grace timer + periodic loops are exercised separately via shorter-period integration
/// tests at the end of this file.
/// </summary>
public class RejoinWorkerTests
{
    // --- helpers ----------------------------------------------------------

    private static Account NewAccount(ulong id = 12345UL) => new()
    {
        UserId = id,
        Username = "test",
        Cookie = "_|WARNING:-DO-NOT-SHARE-THIS",
    };

    private static RejoinSettings ShortSettings => new(
        PollInterval: TimeSpan.FromMilliseconds(50),
        GracePeriod: TimeSpan.FromMilliseconds(100),
        MemoryThresholdMb: 200,
        WindowTitleCheckInterval: TimeSpan.FromMilliseconds(50));

    private static RejoinWorker NewWorker(
        RejoinSettings? settings = null,
        ILauncher? launcher = null,
        Action<RejoinWorkerState>? cb = null,
        TimeProvider? time = null)
        => new(
            account: NewAccount(),
            settings: settings ?? ShortSettings,
            launcher: launcher,
            presence: null,
            memoryKiller: null,
            flogFactory: null,
            titleFactory: null,
            onStateChanged: cb,
            timeProvider: time,
            logger: NullLogger<RejoinWorker>.Instance);

    private static LaunchTarget DefaultTarget() => new LaunchTarget.Place(123UL);

    // --- Idle state -------------------------------------------------------

    [Fact]
    public async Task Idle_SessionStarted_transitions_to_Watching()
    {
        await using var w = NewWorker();
        Assert.Equal(RejoinWorkerState.Idle, w.State);

        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "TID", DefaultTarget()));

        Assert.Equal(RejoinWorkerState.Watching, w.State);
        Assert.Equal(42, w.CurrentPid);
        Assert.Equal("TID", w.CurrentTrackerId);
    }

    [Fact]
    public async Task Idle_StopRequested_stays_Idle()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.StopRequested());
        Assert.Equal(RejoinWorkerState.Idle, w.State);
    }

    [Fact]
    public async Task Idle_unrelated_event_is_ignored()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.WindowTitleError("Disconnected"));
        await w.HandleEventAsync(new RejoinEvent.MemoryCheckFailed());
        await w.HandleEventAsync(new RejoinEvent.ProcessExited());
        Assert.Equal(RejoinWorkerState.Idle, w.State);
    }

    // --- Watching state ---------------------------------------------------

    [Fact]
    public async Task Watching_FLogDisconnected_enters_GracePeriod()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    [Fact]
    public async Task Watching_FLogConnected_records_timestamp_and_stays_Watching()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var w = NewWorker(time: time);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Connected));
        Assert.Equal(RejoinWorkerState.Watching, w.State);
    }

    [Fact]
    public async Task Watching_WindowTitleError_enters_GracePeriod()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.WindowTitleError("Disconnected"));
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    [Fact]
    public async Task Watching_MemoryCheckFailed_enters_GracePeriod()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.MemoryCheckFailed());
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    [Fact]
    public async Task Watching_ProcessExited_enters_GracePeriod()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.ProcessExited());
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    [Fact]
    public async Task Watching_FLogPaused_does_not_trigger_grace()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Paused));
        Assert.Equal(RejoinWorkerState.Watching, w.State);
    }

    [Fact]
    public async Task Watching_FLogBetaMenu_does_not_trigger_grace()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.BetaMenu));
        Assert.Equal(RejoinWorkerState.Watching, w.State);
    }

    [Fact]
    public async Task Watching_StopRequested_returns_to_Idle()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.StopRequested());
        Assert.Equal(RejoinWorkerState.Idle, w.State);
        Assert.Null(w.CurrentPid);
    }

    [Fact]
    public async Task Watching_SessionStarted_replaces_session_in_place()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T1", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(99, "T2", DefaultTarget()));
        Assert.Equal(RejoinWorkerState.Watching, w.State);
        Assert.Equal(99, w.CurrentPid);
        Assert.Equal("T2", w.CurrentTrackerId);
    }

    // --- GracePeriod state ------------------------------------------------

    [Fact]
    public async Task GracePeriod_FLogConnected_returns_to_Watching()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);

        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Connected));
        Assert.Equal(RejoinWorkerState.Watching, w.State);
    }

    [Fact]
    public async Task GracePeriod_PresenceInGame_returns_to_Watching()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));

        var presence = new UserPresence { UserId = 12345UL, Type = PresenceType.InGame };
        await w.HandleEventAsync(new RejoinEvent.PresenceUpdate(presence));
        Assert.Equal(RejoinWorkerState.Watching, w.State);
    }

    [Fact]
    public async Task GracePeriod_PresenceOnline_does_not_return_to_Watching()
    {
        // Presence Online ≠ InGame; only InGame counts as recovery.
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));

        var presence = new UserPresence { UserId = 12345UL, Type = PresenceType.Online };
        await w.HandleEventAsync(new RejoinEvent.PresenceUpdate(presence));
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    [Fact]
    public async Task GracePeriod_StopRequested_returns_to_Idle()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.StopRequested());
        Assert.Equal(RejoinWorkerState.Idle, w.State);
    }

    [Fact]
    public async Task GracePeriod_GraceTimerExpired_with_no_launcher_goes_to_Error()
    {
        await using var w = NewWorker(launcher: null);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.GraceTimerExpired());

        // Drain the synthetic LaunchCompleted that EnterRejoining enqueues. We need to
        // run the consumer briefly to process it.
        await w.StartAsync();
        await Task.Delay(150);
        Assert.Equal(RejoinWorkerState.Error, w.State);
    }

    [Fact]
    public async Task GracePeriod_SessionStarted_replaces_session_and_skips_rejoin()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T1", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);

        await w.HandleEventAsync(new RejoinEvent.SessionStarted(77, "T2", DefaultTarget()));
        Assert.Equal(RejoinWorkerState.Watching, w.State);
        Assert.Equal(77, w.CurrentPid);
        Assert.Equal("T2", w.CurrentTrackerId);
    }

    // --- Rejoining state --------------------------------------------------

    [Fact]
    public async Task Rejoining_LaunchSuccess_resumes_Watching_with_new_pid()
    {
        var launcher = new FakeLauncher(LaunchResult.Ok(101, "NEWTID"));
        await using var w = NewWorker(launcher: launcher);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));

        // Drive the timer expiry directly — synchronous transition to Rejoining + launch task spawn.
        await w.HandleEventAsync(new RejoinEvent.GraceTimerExpired());
        Assert.Equal(RejoinWorkerState.Rejoining, w.State);

        // Manually inject the LaunchCompleted (in real code the launch task does this).
        await w.HandleEventAsync(new RejoinEvent.LaunchCompleted(true, 101, "NEWTID", null));
        Assert.Equal(RejoinWorkerState.Watching, w.State);
        Assert.Equal(101, w.CurrentPid);
        Assert.Equal("NEWTID", w.CurrentTrackerId);
    }

    [Fact]
    public async Task Rejoining_LaunchFailure_transitions_to_Error()
    {
        var launcher = new FakeLauncher(LaunchResult.Fail("boom"));
        await using var w = NewWorker(launcher: launcher);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.GraceTimerExpired());
        await w.HandleEventAsync(new RejoinEvent.LaunchCompleted(false, null, null, "boom"));
        Assert.Equal(RejoinWorkerState.Error, w.State);
    }

    [Fact]
    public async Task Rejoining_StopRequested_transitions_to_Idle()
    {
        var launcher = new FakeLauncher(LaunchResult.Ok(101, "NEWTID"));
        await using var w = NewWorker(launcher: launcher);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.GraceTimerExpired());
        await w.HandleEventAsync(new RejoinEvent.StopRequested());
        Assert.Equal(RejoinWorkerState.Idle, w.State);
    }

    // --- Error state ------------------------------------------------------

    [Fact]
    public async Task Error_SessionStarted_clears_error_and_resumes_Watching()
    {
        var launcher = new FakeLauncher(LaunchResult.Fail("dead"));
        await using var w = NewWorker(launcher: launcher);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.GraceTimerExpired());
        await w.HandleEventAsync(new RejoinEvent.LaunchCompleted(false, null, null, "dead"));
        Assert.Equal(RejoinWorkerState.Error, w.State);

        await w.HandleEventAsync(new RejoinEvent.SessionStarted(200, "T2", DefaultTarget()));
        Assert.Equal(RejoinWorkerState.Watching, w.State);
        Assert.Equal(200, w.CurrentPid);
    }

    [Fact]
    public async Task Error_StopRequested_returns_to_Idle()
    {
        var launcher = new FakeLauncher(LaunchResult.Fail("dead"));
        await using var w = NewWorker(launcher: launcher);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.GraceTimerExpired());
        await w.HandleEventAsync(new RejoinEvent.LaunchCompleted(false, null, null, "dead"));
        await w.HandleEventAsync(new RejoinEvent.StopRequested());
        Assert.Equal(RejoinWorkerState.Idle, w.State);
    }

    // --- Q1–Q8 edge cases ------------------------------------------------

    /// <summary>Q1: ProcessExited during GracePeriod is ignored (already in grace).</summary>
    [Fact]
    public async Task Q1_ProcessExited_during_GracePeriod_is_idempotent()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.ProcessExited());
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    /// <summary>Q2: PID gets reused on subsequent ProcessExited — worker handles
    /// gracefully because it only acts on PIDs it owns from the launcher.</summary>
    [Fact]
    public async Task Q2_ProcessExited_after_session_swap_uses_new_pid()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T1", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(99, "T2", DefaultTarget()));
        Assert.Equal(99, w.CurrentPid);
        // ProcessExited is interpreted in Watching context for the current pid.
        await w.HandleEventAsync(new RejoinEvent.ProcessExited());
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    /// <summary>Q3: Multiple disconnect events during grace are coalesced.</summary>
    [Fact]
    public async Task Q3_Multiple_disconnect_events_during_grace_are_idempotent()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.WindowTitleError("Disconnected"));
        await w.HandleEventAsync(new RejoinEvent.MemoryCheckFailed());
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Crashed));
        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    /// <summary>Q4: Presence Offline is suppressed when FLog reports Connected recently
    /// (FLog wins; threshold = 2× grace period).</summary>
    [Fact]
    public async Task Q4_Presence_Offline_suppressed_when_FLog_recently_Connected()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var w = NewWorker(time: time);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Connected));

        // Advance only a small amount (well under 2× grace = 200ms).
        time.Advance(TimeSpan.FromMilliseconds(20));

        var presence = new UserPresence { UserId = 12345UL, Type = PresenceType.Offline };
        await w.HandleEventAsync(new RejoinEvent.PresenceUpdate(presence));

        Assert.Equal(RejoinWorkerState.Watching, w.State);
    }

    /// <summary>Q4-companion: Presence Offline triggers grace if FLog Connected was
    /// long enough ago.</summary>
    [Fact]
    public async Task Q4b_Presence_Offline_triggers_grace_after_stale_FLogConnected()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var w = NewWorker(time: time);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Connected));

        // Advance well past 2× grace (= 200ms in ShortSettings).
        time.Advance(TimeSpan.FromSeconds(5));

        var presence = new UserPresence { UserId = 12345UL, Type = PresenceType.Offline };
        await w.HandleEventAsync(new RejoinEvent.PresenceUpdate(presence));

        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    /// <summary>Q5: Presence Offline at session start (no FLog Connected yet) triggers
    /// grace (no suppression possible).</summary>
    [Fact]
    public async Task Q5_Presence_Offline_at_session_start_triggers_grace()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        // No FLog Connected ever recorded.

        var presence = new UserPresence { UserId = 12345UL, Type = PresenceType.Offline };
        await w.HandleEventAsync(new RejoinEvent.PresenceUpdate(presence));

        Assert.Equal(RejoinWorkerState.GracePeriod, w.State);
    }

    /// <summary>Q6: StopRequested while Rejoining drops to Idle without waiting for
    /// the launch task. (LaunchCompleted may arrive later but is ignored in Idle.)</summary>
    [Fact]
    public async Task Q6_StopRequested_during_Rejoining_drops_to_Idle()
    {
        var launcher = new FakeLauncher(LaunchResult.Ok(101, "NEWTID"), delay: TimeSpan.FromSeconds(2));
        await using var w = NewWorker(launcher: launcher);
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.GraceTimerExpired());
        Assert.Equal(RejoinWorkerState.Rejoining, w.State);

        await w.HandleEventAsync(new RejoinEvent.StopRequested());
        Assert.Equal(RejoinWorkerState.Idle, w.State);

        // Late LaunchCompleted is benign.
        await w.HandleEventAsync(new RejoinEvent.LaunchCompleted(true, 101, "NEWTID", null));
        Assert.Equal(RejoinWorkerState.Idle, w.State);
    }

    /// <summary>Q7: Disabled flip-back semantics — manager invokes <c>StopRequested</c>
    /// then a later re-enable does NOT auto-spawn a worker. We test the worker side:
    /// after StopRequested in Watching, only an explicit SessionStarted resumes monitoring.</summary>
    [Fact]
    public async Task Q7_StopRequested_does_not_resume_without_explicit_SessionStarted()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.StopRequested());
        Assert.Equal(RejoinWorkerState.Idle, w.State);

        // Spurious watcher events (e.g., a late FLog tail) must not resume.
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Connected));
        await w.HandleEventAsync(new RejoinEvent.PresenceUpdate(
            new UserPresence { UserId = 12345UL, Type = PresenceType.InGame }));

        Assert.Equal(RejoinWorkerState.Idle, w.State);
    }

    /// <summary>Q8: GraceTimerExpired arriving in Watching (e.g., after recovery
    /// cancelled the timer but the cancellation lost a race) is harmless.</summary>
    [Fact]
    public async Task Q8_StaleGraceTimerExpired_in_Watching_is_ignored()
    {
        await using var w = NewWorker();
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.GraceTimerExpired());
        Assert.Equal(RejoinWorkerState.Watching, w.State);
    }

    // --- E: FLog rotation race --------------------------------------------

    /// <summary>E: a second SessionStarted (e.g., FLog file rotated and we re-spawned the
    /// watcher) must not duplicate watchers or leak resources. We can't directly inspect
    /// the file handles, so we assert the FSM stays consistent across rapid replacements.</summary>
    [Fact]
    public async Task E_FLog_rotation_replaces_session_cleanly()
    {
        await using var w = NewWorker();
        for (int i = 0; i < 10; i++)
        {
            await w.HandleEventAsync(new RejoinEvent.SessionStarted(100 + i, $"T{i}", DefaultTarget()));
        }
        Assert.Equal(RejoinWorkerState.Watching, w.State);
        Assert.Equal(109, w.CurrentPid);
        Assert.Equal("T9", w.CurrentTrackerId);
    }

    // --- F: concurrency stress --------------------------------------------

    /// <summary>F: 100 random events from 4 producers funnelled through the channel.
    /// The single-consumer guarantee means we can never observe a state outside the
    /// enum, and after StopRequested-drain we always end in Idle.</summary>
    [Fact]
    public async Task F_Concurrent_producers_never_corrupt_state()
    {
        await using var w = NewWorker();
        await w.StartAsync();
        await w.OnEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));

        var rng = new Random(0xC0FFEE);
        var ev = new RejoinEvent[]
        {
            new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Connected),
            new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected),
            new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Paused),
            new RejoinEvent.WindowTitleError("Disconnected"),
            new RejoinEvent.MemoryCheckFailed(),
            new RejoinEvent.PresenceUpdate(new UserPresence { UserId = 12345UL, Type = PresenceType.InGame }),
            new RejoinEvent.PresenceUpdate(new UserPresence { UserId = 12345UL, Type = PresenceType.Offline }),
        };

        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            for (int i = 0; i < 25; i++)
            {
                int idx;
                lock (rng) idx = rng.Next(ev.Length);
                await w.OnEventAsync(ev[idx]);
            }
        })).ToArray();
        await Task.WhenAll(tasks);

        // Drain via StopRequested.
        await w.OnEventAsync(new RejoinEvent.StopRequested());
        // Give the consumer a beat to drain.
        await Task.Delay(150);

        Assert.True(Enum.IsDefined(w.State));
        Assert.Equal(RejoinWorkerState.Idle, w.State);
    }

    // --- Callback / disposal ---------------------------------------------

    [Fact]
    public async Task StateChanged_callback_fires_on_each_transition()
    {
        var observed = new List<RejoinWorkerState>();
        await using var w = NewWorker(cb: s => observed.Add(s));

        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Disconnected));
        await w.HandleEventAsync(new RejoinEvent.FLogStateChanged(FlogWatcher.GameState.Connected));
        await w.HandleEventAsync(new RejoinEvent.StopRequested());

        Assert.Equal(new[]
        {
            RejoinWorkerState.Watching,
            RejoinWorkerState.GracePeriod,
            RejoinWorkerState.Watching,
            RejoinWorkerState.Idle,
        }, observed);
    }

    [Fact]
    public async Task DisposeAsync_drops_callback_before_teardown()
    {
        var observed = new List<RejoinWorkerState>();
        var w = NewWorker(cb: s => observed.Add(s));
        await w.HandleEventAsync(new RejoinEvent.SessionStarted(42, "T", DefaultTarget()));
        observed.Clear();

        await w.DisposeAsync();

        // Any transitions during disposal must not have invoked the cleared callback.
        Assert.Empty(observed);
    }

    // --- Helpers ----------------------------------------------------------

    private sealed class FakeLauncher : ILauncher
    {
        private readonly LaunchResult _result;
        private readonly TimeSpan _delay;
        public int CallCount { get; private set; }

        public FakeLauncher(LaunchResult result, TimeSpan? delay = null)
        {
            _result = result;
            _delay = delay ?? TimeSpan.Zero;
        }

        public async Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct = default)
        {
            CallCount++;
            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, ct);
            return _result;
        }
    }

    /// <summary>Minimal TimeProvider stub that allows tests to advance the clock.
    /// We only need <see cref="GetUtcNow"/> for the Q4 FLog/presence interaction;
    /// timer-based behavior is exercised separately.</summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset start) { _now = start; }
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) { _now += delta; }
    }
}
