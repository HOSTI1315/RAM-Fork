namespace RAM.Core.Models;

/// <summary>
/// Auto-rejoin worker FSM state. Independent from <see cref="AccountStatus"/> —
/// AccountStatus reflects presence/auth state, RejoinWorkerState reflects the
/// auto-rejoin watcher's lifecycle. UI may show both simultaneously (e.g. status
/// dot for presence + clock icon for GracePeriod).
/// </summary>
public enum RejoinWorkerState
{
    /// <summary>No active monitoring. Initial; reached after clean process close,
    /// manual stop, or fatal launch failure.</summary>
    Idle = 0,

    /// <summary>Process alive, all signals being polled.</summary>
    Watching = 1,

    /// <summary>Disconnect signal observed; countdown timer running before relaunch.</summary>
    GracePeriod = 2,

    /// <summary>ILauncher.LaunchAsync in progress; other events queued/ignored.</summary>
    Rejoining = 3,

    /// <summary>Launch failed; no auto-retry. User must intervene.</summary>
    Error = 4,
}
