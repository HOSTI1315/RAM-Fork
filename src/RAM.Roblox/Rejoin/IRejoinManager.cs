using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Roblox.Rejoin;

/// <summary>
/// Singleton service that owns one <c>RejoinWorker</c> per launched account
/// (keyed by <see cref="Account.UserId"/>). Bridges between launch flow and
/// worker lifecycle.
///
/// <para>Note: re-enabling a previously-disabled account does NOT auto-launch.
/// The worker is reactivated only when the user manually launches again — manager
/// posts <see cref="RejoinEvent.SessionStarted"/> only from <see cref="OnAccountLaunched"/>.</para>
/// </summary>
public interface IRejoinManager
{
    /// <summary>
    /// Notify that an account just launched successfully. Manager creates or reuses
    /// a worker for the account and enqueues <see cref="RejoinEvent.SessionStarted"/>.
    /// </summary>
    /// <param name="workerStateChanged">
    /// Optional callback invoked on every FSM transition. Caller marshals to UI thread
    /// if needed (e.g. <c>Application.Current.Dispatcher.InvokeAsync</c>).
    /// v1: single callback consumer (UI). Future versions may evolve to
    /// <c>IObservable&lt;RejoinWorkerState&gt;</c> for multi-consumer subscription
    /// (UI + logging + plugins).
    /// </param>
    void OnAccountLaunched(
        Account account,
        LaunchResult result,
        LaunchTarget target,
        Action<RejoinWorkerState>? workerStateChanged = null);

    /// <summary>Stop monitoring this account but keep the worker cached (re-enable
    /// without auto-launch — only the next manual launch reactivates).</summary>
    void OnAccountDisabled(ulong userId);

    /// <summary>Stop and remove the worker entirely (account removed from store).</summary>
    Task OnAccountRemovedAsync(ulong userId);

    /// <summary>Stop all workers gracefully. Called from app shutdown hooks.</summary>
    Task ShutdownAsync(CancellationToken ct = default);
}
