using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.Watchers;

namespace RAM.Roblox.Rejoin;

/// <summary>
/// Discriminated union of events that drive the <see cref="RejoinWorker"/> FSM.
/// All events are funnelled through a single-consumer channel inside the worker
/// — producers (watchers, timers, launch tasks) only enqueue; the consumer
/// applies state transitions serially. No locks needed for state mutations.
/// </summary>
public abstract record RejoinEvent
{
    /// <summary>External: launch (manual or initial) succeeded for this account.</summary>
    public sealed record SessionStarted(int Pid, string TrackerId, LaunchTarget Target) : RejoinEvent;

    /// <summary>External: stop monitoring (account disabled / removed / app shutdown).</summary>
    public sealed record StopRequested : RejoinEvent;

    /// <summary>From <see cref="FlogWatcher"/>: game state changed.</summary>
    public sealed record FLogStateChanged(FlogWatcher.GameState State) : RejoinEvent;

    /// <summary>From <see cref="WindowTitleWatcher"/>: title now matches a crash pattern.</summary>
    public sealed record WindowTitleError(string Marker) : RejoinEvent;

    /// <summary>From periodic <see cref="IPresenceProvider"/> poll.</summary>
    public sealed record PresenceUpdate(UserPresence Presence) : RejoinEvent;

    /// <summary>From periodic <see cref="MemoryThresholdKiller.CheckAndKill"/> returning true.</summary>
    public sealed record MemoryCheckFailed : RejoinEvent;

    /// <summary>From process tracking: the tracked PID exited.</summary>
    public sealed record ProcessExited : RejoinEvent;

    /// <summary>Internal: grace timer ran to completion (no recovery during the window).</summary>
    public sealed record GraceTimerExpired : RejoinEvent;

    /// <summary>From the relaunch task continuation (success/failure).</summary>
    public sealed record LaunchCompleted(bool Success, int? Pid, string? TrackerId, string? Error) : RejoinEvent;
}
