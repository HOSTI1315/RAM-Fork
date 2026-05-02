using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.Watchers;

namespace RAM.Roblox.Rejoin;

/// <summary>
/// Singleton bridge between the launch flow and per-account <see cref="RejoinWorker"/>s.
/// Owns one worker per launched account (keyed by <see cref="Account.UserId"/>) and
/// forwards launch / disable / remove events.
///
/// <para>Settings are snapshotted at construction; hot-reload requires app restart per
/// v1 design. The Settings UI surfaces this constraint to users.</para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RejoinManager : IRejoinManager, IAsyncDisposable
{
    private readonly ConcurrentDictionary<ulong, RejoinWorker> _workers = new();
    private readonly ILauncher _launcher;
    private readonly IPresenceProvider _presence;
    private readonly RejoinSettings _settings;
    private readonly MemoryThresholdKiller _memoryKiller;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RejoinManager> _logger;
    private readonly TimeProvider _time;

    public RejoinManager(
        IOptions<AppSettings> settings,
        ILauncher launcher,
        IPresenceProvider presence,
        ILoggerFactory loggerFactory,
        TimeProvider? timeProvider = null)
    {
        _launcher = launcher;
        _presence = presence;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<RejoinManager>();
        _time = timeProvider ?? TimeProvider.System;
        _settings = RejoinSettings.FromAppSettings(settings.Value);
        _memoryKiller = new MemoryThresholdKiller(
            _settings.MemoryThresholdMb,
            loggerFactory.CreateLogger<MemoryThresholdKiller>());
    }

    /// <summary>How many workers are currently tracked. Visible for diagnostics + tests.</summary>
    public int WorkerCount => _workers.Count;

    /// <summary>Snapshot of state per known userId — for tests / smoke flows.</summary>
    public IReadOnlyDictionary<ulong, RejoinWorkerState> Snapshot()
        => _workers.ToDictionary(kv => kv.Key, kv => kv.Value.State);

    public void OnAccountLaunched(
        Account account,
        LaunchResult result,
        LaunchTarget target,
        Action<RejoinWorkerState>? workerStateChanged = null)
    {
        if (!result.IsSuccess || result.ProcessId is not int pid || result.BrowserTrackerId is not string tid)
        {
            _logger.LogTrace(
                "OnAccountLaunched skipped — unsuccessful or missing pid/trackerId for {UserId}",
                account.UserId);
            return;
        }

        var worker = _workers.GetOrAdd(account.UserId, _ => CreateWorker(account, workerStateChanged));
        _ = worker.StartAsync();
        // Fire SessionStarted asynchronously — channel write is non-blocking but we don't
        // want to await on a UI thread.
        _ = Task.Run(async () =>
        {
            try { await worker.OnEventAsync(new RejoinEvent.SessionStarted(pid, tid, target)); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue SessionStarted for {UserId}", account.UserId);
            }
        });
    }

    public void OnAccountDisabled(ulong userId)
    {
        if (!_workers.TryGetValue(userId, out var w)) return;
        _ = Task.Run(async () =>
        {
            try { await w.OnEventAsync(new RejoinEvent.StopRequested()); }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to enqueue StopRequested for {UserId}", userId);
            }
        });
    }

    public async Task OnAccountRemovedAsync(ulong userId)
    {
        if (_workers.TryRemove(userId, out var w))
        {
            try { await w.DisposeAsync(); }
            catch (Exception ex) { _logger.LogTrace(ex, "Error disposing worker {UserId}", userId); }
        }
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        var workers = _workers.Values.ToList();
        _workers.Clear();
        foreach (var w in workers)
        {
            try { await w.DisposeAsync(); }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error disposing worker {UserId} during shutdown", w.UserId);
            }
        }
    }

    public ValueTask DisposeAsync() => new(ShutdownAsync());

    private RejoinWorker CreateWorker(Account account, Action<RejoinWorkerState>? callback)
    {
        return new RejoinWorker(
            account: account,
            settings: _settings,
            launcher: _launcher,
            presence: _presence,
            memoryKiller: _memoryKiller,
            flogFactory: (pid, explicitPath) =>
            {
                var path = explicitPath ?? FlogWatcher.FindLogFor(pid);
                if (path is null)
                {
                    _logger.LogTrace("No FLog file resolved for pid {Pid}, watcher disabled", pid);
                    return null;
                }
                return new FlogWatcher(
                    path,
                    _loggerFactory.CreateLogger<FlogWatcher>(),
                    _settings.PollInterval);
            },
            titleFactory: pid => new WindowTitleWatcher(
                pid,
                _loggerFactory.CreateLogger<WindowTitleWatcher>(),
                _settings.WindowTitleCheckInterval),
            onStateChanged: callback,
            timeProvider: _time,
            logger: _loggerFactory.CreateLogger<RejoinWorker>());
    }
}
