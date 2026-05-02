using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.Watchers;

namespace RAM.Roblox.Rejoin;

/// <summary>
/// Per-account auto-rejoin worker. Owns a single <see cref="System.Threading.Channels.Channel{T}"/>
/// of <see cref="RejoinEvent"/>s consumed by exactly one task — all FSM transitions execute
/// serially on that consumer, so state mutations need no locking.
///
/// <para>Producers (FLog/title watchers, periodic timers, the launcher continuation) only
/// enqueue events; the consumer applies transitions and starts/stops session-scoped resources.
/// Cancellation is two-tiered: a session CTS is reset on every (re)launch, and a worker-wide
/// shutdown CTS lives for the lifetime of the worker.</para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RejoinWorker : IAsyncDisposable
{
    /// <summary>Factory delegate for FLog watcher. Pid + optional explicit log path.
    /// Returning null disables the FLog watcher for this session.</summary>
    public delegate FlogWatcher? FlogWatcherFactory(int pid, string? explicitLogPath);

    /// <summary>Factory delegate for window-title watcher. Returning null disables it.</summary>
    public delegate WindowTitleWatcher? WindowTitleWatcherFactory(int pid);

    private readonly Account _account;
    private readonly RejoinSettings _settings;
    private readonly ILauncher? _launcher;
    private readonly IPresenceProvider? _presence;
    private readonly MemoryThresholdKiller? _memoryKiller;
    private readonly FlogWatcherFactory? _flogFactory;
    private readonly WindowTitleWatcherFactory? _titleFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<RejoinWorker> _logger;

    private readonly Channel<RejoinEvent> _events = Channel.CreateUnbounded<RejoinEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>Cleared to <c>null</c> by <see cref="DisposeAsync"/> before any teardown,
    /// so late events that race with disposal can't reach the UI thread.</summary>
    private Action<RejoinWorkerState>? _onStateChanged;

    private Task? _consumerTask;
    private RejoinWorkerState _state = RejoinWorkerState.Idle;

    // Session-scoped state (reset on each (re)launch)
    private int? _currentPid;
    private string? _currentTrackerId;
    private LaunchTarget? _lastTarget;
    private DateTimeOffset? _lastFlogConnectedAt;

    // Session-scoped resources (cancelled on stop / restart)
    private FlogWatcher? _flog;
    private WindowTitleWatcher? _titleWatcher;
    private Process? _currentProcess;
    private CancellationTokenSource? _sessionCts;
    private CancellationTokenSource? _graceCts;

    public RejoinWorker(
        Account account,
        RejoinSettings settings,
        ILauncher? launcher,
        IPresenceProvider? presence,
        MemoryThresholdKiller? memoryKiller,
        FlogWatcherFactory? flogFactory,
        WindowTitleWatcherFactory? titleFactory,
        Action<RejoinWorkerState>? onStateChanged,
        TimeProvider? timeProvider,
        ILogger<RejoinWorker> logger)
    {
        _account = account;
        _settings = settings;
        _launcher = launcher;
        _presence = presence;
        _memoryKiller = memoryKiller;
        _flogFactory = flogFactory;
        _titleFactory = titleFactory;
        _onStateChanged = onStateChanged;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    public ulong UserId => _account.UserId;
    public RejoinWorkerState State => _state;
    public int? CurrentPid => _currentPid;
    public string? CurrentTrackerId => _currentTrackerId;

    /// <summary>Starts the consumer task. Idempotent — safe to call repeatedly.</summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_consumerTask is not null) return Task.CompletedTask;
        _consumerTask = Task.Run(() => RunConsumerAsync(_shutdown.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>Public producer entry: enqueue an event for the FSM to consume.</summary>
    public ValueTask OnEventAsync(RejoinEvent ev, CancellationToken ct = default)
        => _events.Writer.WriteAsync(ev, ct);

    /// <summary>Fire-and-forget enqueue for non-async producers (event handlers, timers).</summary>
    private void TryEnqueue(RejoinEvent ev)
    {
        if (!_events.Writer.TryWrite(ev))
            _logger.LogTrace("Channel rejected event {Event} for {UserId}", ev.GetType().Name, _account.UserId);
    }

    private async Task RunConsumerAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var ev in _events.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await HandleEventAsync(ev);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Worker {UserId} crashed handling {Event}",
                        _account.UserId, ev.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    /// <summary>Direct FSM dispatch — exposed as <c>internal</c> for deterministic tests
    /// (tests bypass the channel and drive transitions synchronously).</summary>
    internal async Task HandleEventAsync(RejoinEvent ev)
    {
        switch (_state)
        {
            case RejoinWorkerState.Idle:        await HandleIdleAsync(ev); break;
            case RejoinWorkerState.Watching:    await HandleWatchingAsync(ev); break;
            case RejoinWorkerState.GracePeriod: await HandleGracePeriodAsync(ev); break;
            case RejoinWorkerState.Rejoining:   await HandleRejoiningAsync(ev); break;
            case RejoinWorkerState.Error:       await HandleErrorAsync(ev); break;
        }
    }

    // ---- State handlers --------------------------------------------------

    private Task HandleIdleAsync(RejoinEvent ev)
    {
        switch (ev)
        {
            case RejoinEvent.SessionStarted s:
                StartSession(s.Pid, s.TrackerId, s.Target);
                Transition(RejoinWorkerState.Watching);
                break;
            case RejoinEvent.StopRequested:
                // Already idle; nothing to do.
                break;
            case RejoinEvent.LaunchCompleted lc when lc.Success:
                // Orphan: StopRequested arrived while Rejoining and we dropped to Idle. The
                // launch task that was already in flight just finished and gave us a live
                // process the user no longer wants. Surface in logs so they understand
                // where the stray window came from.
                _logger.LogWarning(
                    "Worker {UserId}: late LaunchCompleted in Idle, ignoring (orphaned pid={Pid})",
                    _account.UserId, lc.Pid);
                break;
            default:
                _logger.LogTrace("Ignoring {Event} in Idle for {UserId}", ev.GetType().Name, _account.UserId);
                break;
        }
        return Task.CompletedTask;
    }

    private Task HandleWatchingAsync(RejoinEvent ev)
    {
        switch (ev)
        {
            case RejoinEvent.SessionStarted s:
                // User manually relaunched while we were already watching; replace session.
                StopSession();
                StartSession(s.Pid, s.TrackerId, s.Target);
                // Stay in Watching.
                break;

            case RejoinEvent.StopRequested:
                StopSession();
                Transition(RejoinWorkerState.Idle);
                break;

            case RejoinEvent.FLogStateChanged fl:
                HandleFlogInWatching(fl.State);
                break;

            case RejoinEvent.WindowTitleError:
                EnterGracePeriod("window-title");
                break;

            case RejoinEvent.PresenceUpdate p:
                HandlePresenceInWatching(p.Presence);
                break;

            case RejoinEvent.MemoryCheckFailed:
                // Killer already terminated the process; treat as imminent disconnect.
                EnterGracePeriod("memory-threshold");
                break;

            case RejoinEvent.ProcessExited:
                EnterGracePeriod("process-exit");
                break;

            default:
                _logger.LogTrace("Ignoring {Event} in Watching for {UserId}", ev.GetType().Name, _account.UserId);
                break;
        }
        return Task.CompletedTask;
    }

    private Task HandleGracePeriodAsync(RejoinEvent ev)
    {
        switch (ev)
        {
            case RejoinEvent.StopRequested:
                CancelGraceTimer();
                StopSession();
                Transition(RejoinWorkerState.Idle);
                break;

            case RejoinEvent.FLogStateChanged { State: FlogWatcher.GameState.Connected }:
                // Recovered. Cancel grace, return to Watching.
                CancelGraceTimer();
                _lastFlogConnectedAt = _time.GetUtcNow();
                Transition(RejoinWorkerState.Watching);
                break;

            case RejoinEvent.PresenceUpdate { Presence.Type: PresenceType.InGame }:
                CancelGraceTimer();
                Transition(RejoinWorkerState.Watching);
                break;

            case RejoinEvent.GraceTimerExpired:
                EnterRejoining();
                break;

            case RejoinEvent.SessionStarted s:
                // User manually relaunched mid-grace; honor it and skip auto-rejoin.
                CancelGraceTimer();
                StopSession();
                StartSession(s.Pid, s.TrackerId, s.Target);
                Transition(RejoinWorkerState.Watching);
                break;

            default:
                _logger.LogTrace("Ignoring {Event} in GracePeriod for {UserId}",
                    ev.GetType().Name, _account.UserId);
                break;
        }
        return Task.CompletedTask;
    }

    private Task HandleRejoiningAsync(RejoinEvent ev)
    {
        switch (ev)
        {
            case RejoinEvent.LaunchCompleted lc when lc.Success && lc.Pid is int pid && lc.TrackerId is string tid:
                StartSession(pid, tid, _lastTarget!);
                Transition(RejoinWorkerState.Watching);
                break;

            case RejoinEvent.LaunchCompleted lc:
                _logger.LogWarning("Auto-rejoin launch failed for {UserId}: {Error}",
                    _account.UserId, lc.Error);
                Transition(RejoinWorkerState.Error);
                break;

            case RejoinEvent.StopRequested:
                // Don't interrupt the launch task — just remember to drop to Idle when it
                // finishes. We can't actually stop ILauncher mid-flight without surgery, so
                // we mark intent via the shutdown CTS and let the LaunchCompleted handler drop us.
                _shutdown.Cancel();
                Transition(RejoinWorkerState.Idle);
                break;

            default:
                _logger.LogTrace("Ignoring {Event} in Rejoining for {UserId}",
                    ev.GetType().Name, _account.UserId);
                break;
        }
        return Task.CompletedTask;
    }

    private Task HandleErrorAsync(RejoinEvent ev)
    {
        switch (ev)
        {
            case RejoinEvent.SessionStarted s:
                // Manual relaunch clears the error.
                StartSession(s.Pid, s.TrackerId, s.Target);
                Transition(RejoinWorkerState.Watching);
                break;
            case RejoinEvent.StopRequested:
                ClearSessionState();
                Transition(RejoinWorkerState.Idle);
                break;
            default:
                _logger.LogTrace("Ignoring {Event} in Error for {UserId}", ev.GetType().Name, _account.UserId);
                break;
        }
        return Task.CompletedTask;
    }

    // ---- Watching sub-helpers --------------------------------------------

    private void HandleFlogInWatching(FlogWatcher.GameState state)
    {
        switch (state)
        {
            case FlogWatcher.GameState.Connected:
                _lastFlogConnectedAt = _time.GetUtcNow();
                break;
            case FlogWatcher.GameState.Disconnected:
            case FlogWatcher.GameState.Crashed:
                EnterGracePeriod($"flog:{state}");
                break;
            case FlogWatcher.GameState.Paused:
            case FlogWatcher.GameState.BetaMenu:
                // User-initiated, not a crash. Don't trigger relaunch.
                break;
            default:
                // Initializing / Unknown — keep watching.
                break;
        }
    }

    private void HandlePresenceInWatching(UserPresence presence)
    {
        if (presence.Type != PresenceType.Offline) return;

        // FLog is more authoritative than presence. If we recently saw a Connected FLog
        // marker, presence Offline is stale — ignore it. Threshold = 2× grace period.
        if (_lastFlogConnectedAt is { } lastSeen)
        {
            var elapsed = _time.GetUtcNow() - lastSeen;
            if (elapsed < _settings.GracePeriod * 2)
            {
                _logger.LogTrace(
                    "Presence Offline for {UserId} but FLog Connected {Elapsed} ago — ignoring",
                    _account.UserId, elapsed);
                return;
            }
        }
        EnterGracePeriod("presence-offline");
    }

    // ---- Transitions / lifecycle ----------------------------------------

    private void Transition(RejoinWorkerState next)
    {
        if (_state == next) return;
        var prev = _state;
        _state = next;
        _logger.LogDebug("Worker {UserId}: {Prev} -> {Next}", _account.UserId, prev, next);

        try
        {
            _onStateChanged?.Invoke(next);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WorkerStateChanged callback threw for {UserId}", _account.UserId);
        }
    }

    private void StartSession(int pid, string trackerId, LaunchTarget target)
    {
        _currentPid = pid;
        _currentTrackerId = trackerId;
        _lastTarget = target;
        _lastFlogConnectedAt = null;

        _sessionCts = new CancellationTokenSource();
        var token = _sessionCts.Token;

        // FLog watcher
        if (_flogFactory is not null)
        {
            _flog = _flogFactory(pid, null);
            if (_flog is not null)
            {
                _flog.StateChanged += OnFlogStateChanged;
                _ = _flog.StartAsync(token);
            }
        }

        // Window-title watcher
        if (_titleFactory is not null)
        {
            _titleWatcher = _titleFactory(pid);
            if (_titleWatcher is not null)
            {
                _titleWatcher.CrashDetected += OnWindowTitleError;
                _ = _titleWatcher.StartAsync(token);
            }
        }

        // Process exit subscription. Primary signal — fires the moment the kernel
        // notifies us. The memory-check loop's HasExited probe is the fallback if
        // event subscription failed (e.g. permissions). Synthetic PIDs in tests
        // hit ArgumentException and skip silently — tests drive ProcessExited via
        // HandleEventAsync directly when they want to exercise that path.
        try
        {
            _currentProcess = Process.GetProcessById(pid);
            _currentProcess.EnableRaisingEvents = true;
            _currentProcess.Exited += OnProcessExitedHandler;
            if (_currentProcess.HasExited)
                TryEnqueue(new RejoinEvent.ProcessExited());
        }
        catch (ArgumentException)
        {
            // PID not real (test) or already dead. Memory-loop fallback covers production;
            // tests don't care about exit detection here.
            _currentProcess = null;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not subscribe to Process.Exited for pid {Pid}", pid);
        }

        // Periodic memory check (also doubles as the HasExited fallback)
        if (_memoryKiller is not null)
            _ = Task.Run(() => RunMemoryCheckLoopAsync(pid, token), CancellationToken.None);

        // Periodic presence poll
        if (_presence is not null)
            _ = Task.Run(() => RunPresencePollLoopAsync(token), CancellationToken.None);
    }

    private void StopSession()
    {
        _sessionCts?.Cancel();

        if (_flog is not null)
        {
            _flog.StateChanged -= OnFlogStateChanged;
            _flog.Dispose();
            _flog = null;
        }
        if (_titleWatcher is not null)
        {
            _titleWatcher.CrashDetected -= OnWindowTitleError;
            _titleWatcher.Dispose();
            _titleWatcher = null;
        }
        if (_currentProcess is not null)
        {
            try { _currentProcess.Exited -= OnProcessExitedHandler; } catch { /* ignore */ }
            try { _currentProcess.Dispose(); } catch { /* ignore */ }
            _currentProcess = null;
        }
        _sessionCts?.Dispose();
        _sessionCts = null;
        ClearSessionState();
    }

    private void ClearSessionState()
    {
        _currentPid = null;
        _currentTrackerId = null;
        _lastFlogConnectedAt = null;
        // _lastTarget intentionally retained — used by Rejoining → Watching transition.
    }

    private void EnterGracePeriod(string reason)
    {
        if (_state == RejoinWorkerState.GracePeriod) return;

        _logger.LogInformation(
            "Worker {UserId}: entering grace period ({Reason}) for {Grace}",
            _account.UserId, reason, _settings.GracePeriod);

        CancelGraceTimer();
        _graceCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        var token = _graceCts.Token;

        Transition(RejoinWorkerState.GracePeriod);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_settings.GracePeriod, _time, token);
                TryEnqueue(new RejoinEvent.GraceTimerExpired());
            }
            catch (OperationCanceledException) { /* cancelled by recovery or stop */ }
        }, CancellationToken.None);
    }

    private void CancelGraceTimer()
    {
        _graceCts?.Cancel();
        _graceCts?.Dispose();
        _graceCts = null;
    }

    private void EnterRejoining()
    {
        Transition(RejoinWorkerState.Rejoining);
        CancelGraceTimer();

        // Best-effort: kill the old process tree (if still around) before relaunching.
        // entireProcessTree=true catches injector/mod child processes that would otherwise
        // hang and clutter Task Manager.
        if (_currentPid is int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (!p.HasExited)
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(2000))
                        p.Kill(entireProcessTree: true);
                }
            }
            catch (ArgumentException) { /* already gone */ }
            catch (InvalidOperationException) { /* race: self-exited between checks */ }
            catch (Exception ex) { _logger.LogTrace(ex, "Pre-relaunch kill failed for pid {Pid}", pid); }
        }

        // Stop watchers but keep _lastTarget so the LaunchCompleted handler can resume.
        StopWatchersOnly();

        if (_launcher is null || _lastTarget is null)
        {
            TryEnqueue(new RejoinEvent.LaunchCompleted(false, null, null, "No launcher or target"));
            return;
        }

        var target = _lastTarget;
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _launcher.LaunchAsync(
                    new LaunchRequest(_account, target),
                    _shutdown.Token);
                TryEnqueue(new RejoinEvent.LaunchCompleted(
                    result.IsSuccess, result.ProcessId, result.BrowserTrackerId, result.Error));
            }
            catch (OperationCanceledException)
            {
                TryEnqueue(new RejoinEvent.LaunchCompleted(false, null, null, "Cancelled"));
            }
            catch (Exception ex)
            {
                TryEnqueue(new RejoinEvent.LaunchCompleted(false, null, null, ex.Message));
            }
        }, CancellationToken.None);
    }

    private void StopWatchersOnly()
    {
        _sessionCts?.Cancel();
        if (_flog is not null)
        {
            _flog.StateChanged -= OnFlogStateChanged;
            _flog.Dispose();
            _flog = null;
        }
        if (_titleWatcher is not null)
        {
            _titleWatcher.CrashDetected -= OnWindowTitleError;
            _titleWatcher.Dispose();
            _titleWatcher = null;
        }
        if (_currentProcess is not null)
        {
            try { _currentProcess.Exited -= OnProcessExitedHandler; } catch { /* ignore */ }
            try { _currentProcess.Dispose(); } catch { /* ignore */ }
            _currentProcess = null;
        }
        _sessionCts?.Dispose();
        _sessionCts = null;
    }

    // ---- Producer hooks --------------------------------------------------

    private void OnFlogStateChanged(object? sender, FlogWatcher.GameState state)
        => TryEnqueue(new RejoinEvent.FLogStateChanged(state));

    private void OnWindowTitleError(object? sender, string marker)
        => TryEnqueue(new RejoinEvent.WindowTitleError(marker));

    private void OnProcessExitedHandler(object? sender, EventArgs e)
        => TryEnqueue(new RejoinEvent.ProcessExited());

    private async Task RunMemoryCheckLoopAsync(int pid, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(_settings.PollInterval, _time);
            while (await timer.WaitForNextTickAsync(ct))
            {
                // Fallback exit-detection: if Process.Exited subscription failed or fired
                // late, catch it here. The FSM coalesces duplicates via state idempotency.
                if (_currentProcess is { HasExited: true })
                {
                    TryEnqueue(new RejoinEvent.ProcessExited());
                    return;
                }

                if (_memoryKiller is null) return;
                try
                {
                    if (_memoryKiller.CheckAndKill(pid))
                        TryEnqueue(new RejoinEvent.MemoryCheckFailed());
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Memory check loop error for pid {Pid}", pid);
                }
            }
        }
        catch (OperationCanceledException) { /* session ended */ }
    }

    private async Task RunPresencePollLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(_settings.PollInterval, _time);
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (_presence is null) return;
                try
                {
                    var dict = await _presence.GetPresenceAsync(new[] { _account.UserId }, ct);
                    if (dict.TryGetValue(_account.UserId, out var p))
                        TryEnqueue(new RejoinEvent.PresenceUpdate(p));
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Presence poll error for {UserId}", _account.UserId);
                }
            }
        }
        catch (OperationCanceledException) { /* session ended */ }
    }

    // ---- Disposal --------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        // Drop the UI callback BEFORE teardown so any straggler events (mid-flight watchers,
        // late launch continuations) can't reach the consumer.
        _onStateChanged = null;

        _events.Writer.TryComplete();
        _shutdown.Cancel();
        CancelGraceTimer();
        StopSession();

        if (_consumerTask is not null)
        {
            try { await _consumerTask.ConfigureAwait(false); }
            catch { /* swallow — disposal is best-effort */ }
        }
        _shutdown.Dispose();
    }
}
